namespace SamSharp

open System
open Microsoft.ML.OnnxRuntime
open Aardvark.Base
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open Microsoft.ML.OnnxRuntime.Tensors
open Microsoft.FSharp.NativeInterop

#nowarn "9"


[<AutoOpen>]
module Utilities =
    open System.IO
    
    type internal Marker = Marker
    
    let internal get (name : string) =
        let names = typeof<Marker>.Assembly.GetManifestResourceNames() 
        let name = names |> Array.find (fun n -> n.EndsWith name)
        use s = typeof<Marker>.Assembly.GetManifestResourceStream(name)
        use a = new System.IO.Compression.ZipArchive(s, System.IO.Compression.ZipArchiveMode.Read)
        
        let path = Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData)
        let dir = Path.Combine(path, "SamSharp")
        if not (Directory.Exists dir) then Directory.CreateDirectory dir |> ignore
        
        let entry = a.Entries.[0]
        let output = Path.Combine(dir, entry.Name)
        
        use s = a.Entries.[0].Open()
        use fs = File.OpenWrite output
        s.CopyTo fs
        output
            
    module NativeTensor4 =
        let useDense (d : DenseTensor<'a>) (action : NativeTensor4<'a> -> 'x) =
            let delta = d.Strides.ToArray() |> V4i
            let size = d.Dimensions.ToArray() |> V4i
            
            use mem = d.Buffer.Pin()
            action (NativeTensor4<'a>(NativePtr.ofVoidPtr mem.Pointer, Tensor4Info(0L, V4l size, V4l delta)))
            
    module NativeVolume =
        let useDense (d : DenseTensor<'a>) (action : NativeVolume<'a> -> 'x) =
            let delta = d.Strides.ToArray() |> V3i
            let size = d.Dimensions.ToArray() |> V3i
            
            use mem = d.Buffer.Pin()
            action (NativeVolume<'a>(NativePtr.ofVoidPtr mem.Pointer, VolumeInfo(0L, V3l size, V3l delta)))

type Query =
    | Point of point : V2i * label : int
    | Rectangle of Box2i * label : int

      
type SamIndex internal(decoder : InferenceSession, embedding : DenseTensor<float32>, inputSize : V2i, imageSize : V2i) =

    member x.InputSize = inputSize
    member x.ImageSize = imageSize
    
    member x.Query (query : list<Query>) =
        let scale = V2d imageSize / V2d inputSize
        
        let inputPoints, inputLabels =
            let inline transform (pt : V2i) =
                V2d pt * scale |> V2f
                
            let points = 
                query |> List.toArray |> Array.collect (fun q ->
                    match q with
                    | Point(p, l) ->
                        let p = transform p
                        [| p.X; p.Y |]
                    | Rectangle(b,_) ->
                        let l = transform b.Min
                        let h = transform b.Max
                        [| l.X; l.Y; h.X; h.Y |]
                )
            let labels =
                query |> List.toArray |> Array.collect (fun q ->
                    match q with
                    | Point(p, l) ->
                        [| float32 l |]
                    | Rectangle(b,l) ->
                        [| float32 l; float32 l |]
                )
                
            let labels = Array.append labels [|-1.0f|]
            let points = Array.append points [|0.0f; 0.0f|]
            let cnt = labels.Length
            
            DenseTensor(Memory points, ReadOnlySpan [| 1; cnt; 2 |]), DenseTensor(Memory labels, ReadOnlySpan [| 1; cnt |])
            
        let decoderInput =
            [
                NamedOnnxValue.CreateFromTensor("image_embeddings", embedding)
                NamedOnnxValue.CreateFromTensor("point_coords", inputPoints)
                NamedOnnxValue.CreateFromTensor("point_labels", inputLabels)
                NamedOnnxValue.CreateFromTensor("has_mask_input", DenseTensor(Memory [|0.0f|], ReadOnlySpan [|1|]))
                NamedOnnxValue.CreateFromTensor("mask_input", DenseTensor(Memory (Array.zeroCreate<float32> (256*256)), ReadOnlySpan [|1;1;256;256|]))
                NamedOnnxValue.CreateFromTensor("orig_im_size", DenseTensor(Memory [|float32 inputSize.Y; float32 inputSize.X|], ReadOnlySpan [|2|]))
            ]
            
                
        let res = decoder.Run(decoderInput)
        
        let masks = res |> Seq.find (fun r -> r.Name = "masks")
        let masks = masks.Value :?> DenseTensor<float32>
        
        let results = System.Collections.Generic.List<Matrix<float32>>()
        
        NativeTensor4.useDense masks (fun src ->
            for batch in 0 .. int src.SX - 1 do
                for id in 0 .. int src.SY - 1 do
                    let mat = Matrix<float32>(V2i(src.SW, src.SZ))
                    
                    let inline map (v : float32) =
                        1.0f / (1.0f + exp -v)
                    
                    NativeMatrix.using mat (fun dst ->
                        let dst = NativeMatrix(dst.Pointer, dst.Info.Transposed)
                        NativeMatrix.copyWith map src.[batch, id, *, *] dst
                    )
                    
                    results.Add mat
                    
            results.[0]
        )

type Sam(encoder : InferenceSession, decoder : InferenceSession) =
    static let defaultMaxSize = 1024
    static let defaultEncoderPath = lazy (Utilities.get "sam_vit_b_01ec64.encoder.quant.onnx.zip")
    static let defaultDecoderPath = lazy (Utilities.get "sam_vit_b_01ec64.decoder.quant.onnx.zip")

    member x.Encoder = encoder
    member x.Decoder = decoder
    
    member x.Dispose() =
        encoder.Dispose()
        decoder.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()
    
    new(encoder : string, decoder : string, ?options : SessionOptions) =
        
        let dec =
            match options with
            | Some opt -> new InferenceSession(decoder, opt)
            | None -> new InferenceSession(decoder)
        
        let enc =
            match options with
            | Some opt -> new InferenceSession(encoder, opt)
            | None -> new InferenceSession(encoder)
        
        new Sam(enc, dec)
    
    new(?options : SessionOptions) = new Sam(defaultEncoderPath.Value, defaultDecoderPath.Value, ?options = options)
    
    member x.BuildIndex(image : PixImage) =
        let maxSize = defaultMaxSize
    
        let sam = x
        let processingSize =
            let aspect = float image.Size.X / float image.Size.Y
            if aspect > 1.0 then
                V2i(maxSize, int (round (float maxSize / aspect)))
            else
                V2i(int (round (float maxSize * aspect)), maxSize)
                
        let input =
            use img = PixImageSharp.ToImage(image)
            img.Mutate (fun ctx ->
                ctx.Resize(processingSize.X, processingSize.Y) |> ignore  
            )
            let r = img.ToPixImage().ToPixImage<byte>(Col.Format.RGB)
            
            let v = r.Volume
            let dims = [| int v.Size.Y; int v.Size.X; int v.Size.Z|]
            let tensor = DenseTensor(Memory<float32>(Array.zeroCreate (int v.Size.X * int v.Size.Y * int v.Size.Z)), ReadOnlySpan<_>(dims))
            
            NativeVolume.useDense tensor (fun dst ->
                NativeVolume.using (v.Transformed ImageTrafo.Transpose) (fun src ->
                    NativeVolume.copyWith float32 src dst    
                )
            )
            tensor
      
        use res =
            sam.Encoder.Run [
                NamedOnnxValue.CreateFromTensor(sam.Encoder.InputNames.[0], input)
            ]
        let res = res |> Seq.toArray
        let imageEmbedding = res.[0].Value :?> DenseTensor<float32>
        for r in res do r.Dispose()
        
        SamIndex(sam.Decoder, imageEmbedding, image.Size, processingSize)
        
        
    
  