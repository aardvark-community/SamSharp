open System
open Microsoft.ML.OnnxRuntime
open Aardvark.Base
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open Microsoft.ML.OnnxRuntime.Tensors
open Microsoft.FSharp.NativeInterop

#nowarn "9"

type Query =
    | Point of point : V2i * label : int
    | Rectangle of Box2i * label : int

module Utilities =
    open System.IO
    
    let get (name : string) =
        let names = typeof<Query>.Assembly.GetManifestResourceNames() 
        let name = names |> Array.find (fun n -> n.EndsWith name)
        use s = typeof<Query>.Assembly.GetManifestResourceStream(name)
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
  
type SamIndex =
    {
        Decoder : InferenceSession
        Encoded : DenseTensor<float32>
        InputSize : V2i
        ImageSize : V2i
    }
    
    member x.Query (query : list<Query>) =
        let scale = V2d x.ImageSize / V2d x.InputSize
        
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
                NamedOnnxValue.CreateFromTensor("image_embeddings", x.Encoded)
                NamedOnnxValue.CreateFromTensor("point_coords", inputPoints)
                NamedOnnxValue.CreateFromTensor("point_labels", inputLabels)
                NamedOnnxValue.CreateFromTensor("has_mask_input", DenseTensor(Memory [|0.0f|], ReadOnlySpan [|1|]))
                NamedOnnxValue.CreateFromTensor("mask_input", DenseTensor(Memory (Array.zeroCreate<float32> (256*256)), ReadOnlySpan [|1;1;256;256|]))
                NamedOnnxValue.CreateFromTensor("orig_im_size", DenseTensor(Memory [|float32 x.InputSize.Y; float32 x.InputSize.X|], ReadOnlySpan [|2|]))
            ]
            
                
        let res = x.Decoder.Run(decoderInput)
        
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
        
        
  
type Sam(encoder : InferenceSession, decoder : InferenceSession, ?maxSize : int) =
    static let defaultMaxSize = 1024
    static let defaultEncoderPath = lazy (Utilities.get "sam_vit_b_01ec64.encoder.quant.onnx.zip")
    static let defaultDecoderPath = lazy (Utilities.get "sam_vit_b_01ec64.decoder.quant.onnx.zip")

    let maxSize = defaultArg maxSize defaultMaxSize
    
    member x.Encoder = encoder
    member x.Decoder = decoder
    
    member x.Dispose() =
        encoder.Dispose()
        decoder.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()
    
    new(encoder : string, decoder : string, ?maxSize : int) =
        new Sam(new InferenceSession(encoder), new InferenceSession(decoder), ?maxSize = maxSize)
    
    new(?maxSize : int) = new Sam(defaultEncoderPath.Value, defaultDecoderPath.Value, ?maxSize = maxSize)
    
    member x.BuildIndex(image : PixImage) =
        let sam = x
        let processingSize =
            let aspect = float image.Size.X / float image.Size.Y
            if aspect > 1.0 then
                V2i(maxSize, int (round (float maxSize / aspect)))
            else
                V2i(int (round (float maxSize * aspect)), maxSize)
                
        let scaledImage =
            use img = PixImageSharp.ToImage(image)
            img.Mutate (fun ctx ->
                ctx.Resize(processingSize.X, processingSize.Y) |> ignore  
            )
            let r = img.ToPixImage().ToPixImage<byte>()
            let res = PixImage<float32>(Col.Format.RGB, r.Size)
            res.GetMatrix<C3f>().SetMap(r.GetMatrix<C3b>(), fun (v : C3b) -> C3f(float32 v.R, float32 v.G, float32 v.B)) |> ignore
            res
            
        let input =
            let v = scaledImage.Volume
            let dims = [| int v.Size.Y; int v.Size.X; int v.Size.Z|]
            let t = DenseTensor(Memory<float32>(Array.zeroCreate (scaledImage.Size.X * scaledImage.Size.Y * scaledImage.ChannelCount)), ReadOnlySpan<_>(dims))
            
            NativeVolume.useDense t (fun dst ->
                NativeVolume.using (scaledImage.Volume.Transformed ImageTrafo.Transpose) (fun src ->
                    NativeVolume.copy src dst    
                )
            )
            t
            
      
        let inputName = sam.Encoder.InputNames |> Seq.head
        
        let ip = NamedOnnxValue.CreateFromTensor(inputName, input)
        use res = sam.Encoder.Run([ip])
        
        let imageEmbedding = Seq.head(res).Value :?> DenseTensor<float32>
        
        // let t = Tensor4<float32>(V4i(imageEmbedding.Dimensions.ToArray()))
        // NativeTensor4.using t (fun dst ->
        //     NativeTensor4.useDense imageEmbedding (fun src ->
        //         NativeTensor4.copy src dst    
        //     )    
        // )
        {
            Decoder = sam.Decoder
            Encoded = imageEmbedding
            InputSize = image.Size
            ImageSize = scaledImage.Size
        }

        
        
    
        
        
[<EntryPoint>]
let main _args =
    Aardvark.Init()
    use sam = new Sam()
    
    let imagePath = Path.combine [__SOURCE_DIRECTORY__; ".."; "images"; "truck.jpg"]
    
    let image = PixImageSharp.Create imagePath
    Log.startTimed "encode"
    let index = sam.BuildIndex image
    Log.stop()
    
    Log.startTimed "decode"
    let mat =
        index.Query [
            Point(V2i(750, 375), 1)
        ]
    Log.stop()
    
    let result =
        let res = image.ToPixImage<byte>().Copy()
        res.GetMatrix<C4b>().SetMap2 (res.GetMatrix<C4b>(), mat, fun old value ->
            lerp old C4b.Red (0.7 * float value)    
        ) |> ignore
        res
    
    let outputPath = Path.combine [Environment.GetFolderPath Environment.SpecialFolder.Desktop; "segment.png"]
    result.SaveImageSharp outputPath
    
    
    exit 0
    
    
    0