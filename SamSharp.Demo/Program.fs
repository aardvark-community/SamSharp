open System
open Aardvark.Application.Slim
open Aardvark.Base
open Aardvark.Dom
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Giraffe
open Aardvark.Dom.Remote
open Microsoft.ML.OnnxRuntime.Tensors
open Microsoft.ML.OnnxRuntime
open SamSharp.Demo
open Aardium
open BERTTokenizers.Base
open SamSharp
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open Microsoft.FSharp.NativeInterop

#nowarn "9"

let inline get<'a> (name : string) (o : IDisposableReadOnlyCollection<DisposableNamedOnnxValue>)=
    o |> Seq.pick (fun v -> if v.Name = name then Some (v.Value :?> Tensor<'a>) else None)

[<EntryPoint>]
let main args =
    Aardvark.Init()
    // let tok = BERTTokenizers.BertUncasedBaseTokenizer()
    // let t = tok.Tokenize "car and a person."
    //
    // let bert = new InferenceSession("/Users/schorsch/Development/GroundingDINO/bert.onnx")
    //
    // let inputIds = t |> Seq.map (fun struct(_,id,_) -> int64 id) |> Seq.toArray
    // let types = t |> Seq.map (fun struct(_,_,t) -> t) |> Seq.toArray
    // let bertOutput =
    //     bert.Run([
    //         NamedOnnxValue.CreateFromTensor("input_ids", DenseTensor(Memory inputIds, ReadOnlySpan [| 1; inputIds.Length |]))
    //         NamedOnnxValue.CreateFromTensor("token_type_ids", DenseTensor(Memory types, ReadOnlySpan [| 1; inputIds.Length |]))
    //     ])
    //     
    // let encodedText = get<float32> "last_hidden_state" bertOutput
    // // for e in bertOutput do
    // //     match e.Value with
    // //     | :? Tensor<float32> as t ->
    // //         printfn "%s: %A" e.Name (t.Dimensions.ToArray())
    // //     | _ ->
    // //         printfn "%s: %A" e.Name e.ValueType
    // //
    // //
    // let featMap = new InferenceSession("/Users/schorsch/Development/GroundingDINO/feat.onnx")
    // let output =
    //     let lhs = bertOutput |> Seq.find (fun e -> e.Name = "last_hidden_state")
    //     featMap.Run([
    //         NamedOnnxValue.CreateFromTensor("last_hidden_state", lhs.Value :?> Tensor<float32>)
    //     ])
    // for e in output do
    //     match e.Value with
    //     | :? Tensor<float32> as t ->
    //         printfn "%s: %A" e.Name (t.Dimensions.ToArray())
    //     | _ ->
    //         printfn "%s: %A" e.Name e.ValueType
    //
    // let backbone = new InferenceSession("/Users/schorsch/Development/GroundingDINO/backbone.onnx")
    //
    // let img =
    //     use img = PixImageSharp.CreateImage("/Users/schorsch/Development/WorkDir/cre-times-square-1.jpg") //.ToPixImage<byte>(Col.Format.RGB)
    //     img.Mutate(fun ctx ->
    //         ctx.Resize(1199, 800)  |> ignore  
    //     )
    //     printfn "%d x %d" img.Width img.Height
    //     img.ToPixImage().ToPixImage<byte>(Col.Format.RGB)
    //     
    // let imgT = DenseTensor<float32>(ReadOnlySpan [| 1; 3; img.Size.Y; img.Size.X |])
    // NativeTensor4.useDense imgT (fun dst ->
    //     NativeVolume.using img.Volume (fun src ->
    //         let i = src.Info
    //         let src = NativeVolume<byte>(src.Pointer, VolumeInfo(i.Origin, V3l(i.SZ, i.SY, i.SX), V3l(i.DZ, i.DY, i.DX)))
    //         // [0.485, 0.456, 0.406], [0.229, 0.224, 0.225]
    //         let inline conv (v : V3l) (src : nativeptr<byte>) (dst : nativeptr<float32>) =
    //             let res =
    //                 if v.X = 0L then (float32 (NativePtr.read src) / 255.0f - 0.485f) / 0.229f
    //                 elif v.X = 1L then (float32 (NativePtr.read src) / 255.0f - 0.456f) / 0.224f
    //                 else (float32 (NativePtr.read src) / 255.0f - 0.406f) / 0.225f
    //             NativePtr.write dst res
    //         NativeVolume.iterPtr2 conv src dst.[0,*,*,*]    
    //     ) 
    // )
    //
    // let maskT = DenseTensor<bool>(ReadOnlySpan [| 1; img.Size.Y; img.Size.X |])
    //
    // let backOut = 
    //     backbone.Run [
    //         NamedOnnxValue.CreateFromTensor("tensors", imgT)
    //         NamedOnnxValue.CreateFromTensor("mask", maskT)
    //     ]
    // printfn "backOut"
    //
    // let output =
    //     [|
    //         get<float32> "features0" backOut, get<bool> "mask0" backOut, get<float32> "pos0" backOut
    //         get<float32> "features1" backOut, get<bool> "mask1" backOut, get<float32> "pos1" backOut
    //         get<float32> "features2" backOut, get<bool> "mask2" backOut, get<float32> "pos2" backOut
    //     |]
    //
    // let projs = Array.init 4 (fun i -> new InferenceSession($"/Users/schorsch/Development/GroundingDINO/input_proj_{i}.onnx"))
    //
    // let srcs, masks, poss = 
    //     output |> Array.mapi (fun i (f, m, pos) ->
    //         let res =
    //             projs.[i].Run [
    //                 NamedOnnxValue.CreateFromTensor("src", f)
    //             ]
    //         let res = res |> Seq.pick (fun n -> if n.Name = "dst" then Some (n.Value :?> Tensor<float32>) else None)
    //         res, m, pos
    //     ) |> Array.unzip3
    //     
    // let srcs, masks, poss = 
    //     let (f, m, p) = output.[output.Length - 1]
    //     let src =
    //         projs.[3].Run [
    //             NamedOnnxValue.CreateFromTensor("src", f)
    //         ] |> get<float32> "dst"
    //         
    //     printfn "%A" (src.Dimensions.ToArray())
    //     let mask = DenseTensor<bool> [| src.Dimensions.[0]; src.Dimensions.[2]; src.Dimensions.[3] |]
    //     
    //     use pembed = new InferenceSession "/Users/schorsch/Development/GroundingDINO/pembed.onnx"
    //     printfn "%0A" pembed.InputNames
    //     let bb = 
    //         pembed.Run [
    //             //NamedOnnxValue.CreateFromTensor("src", src)
    //             NamedOnnxValue.CreateFromTensor("mask", mask)
    //         ] |> get<float32> "pos_l"
    //         
    //     Array.append srcs [| src |], Array.append masks [|mask|], Array.append poss [|bb|]
    //         
    //     
    // printfn "%0A" (srcs |> Array.map ( fun t -> t.Dimensions.ToArray()))
    // printfn "%0A" (masks |> Array.map ( fun t -> t.Dimensions.ToArray()))
    // printfn "%0A" (poss |> Array.map ( fun t -> t.Dimensions.ToArray()))
    //     
    //     
    // use transformer = new InferenceSession("/Users/schorsch/Development/GroundingDINO/transformer.onnx")
    //     
    //     // text_dict = {
    //     //     "encoded_text": encoded_text,  # bs, 195, d_model
    //     //     "text_token_mask": text_token_mask,  # bs, 195
    //     //     "position_ids": position_ids,  # bs, 195
    //     //     "text_self_attention_masks": text_self_attention_masks,  # bs, 195,195
    //     // }
    // printfn "Inputs:"
    // for KeyValue(k, v) in transformer.InputMetadata do
    //     printfn "  %s: %A %0A" k v.ElementType v.Dimensions 
    //
    // printfn "Outputs:"
    // for KeyValue(k, v) in transformer.OutputMetadata do
    //     printfn "  %s: %A %0A" k v.ElementType v.Dimensions 
    //
    // let text_dict =
    //     Map.ofList [
    //         "encoded_text", encodedText
    //         "text_token_mask", null
    //         "position_ids", null
    //         "text_self_attention_masks", null
    //     ]
    //
    // use output = 
    //     transformer.Run [
    //         NamedOnnxValue.CreateFromTensor("src0", srcs.[0])
    //         NamedOnnxValue.CreateFromTensor("src1", srcs.[1])
    //         NamedOnnxValue.CreateFromTensor("src2", srcs.[2])
    //         NamedOnnxValue.CreateFromTensor("src3", srcs.[3])
    //         NamedOnnxValue.CreateFromTensor("mask0", masks.[0])
    //         NamedOnnxValue.CreateFromTensor("mask1", masks.[1])
    //         NamedOnnxValue.CreateFromTensor("mask2", masks.[2])
    //         NamedOnnxValue.CreateFromTensor("mask3", masks.[3])
    //         NamedOnnxValue.CreateFromTensor("refpoint_embed", DenseTensor<float32>(ReadOnlySpan [| 1; 256; 100; 150 |]))
    //         NamedOnnxValue.CreateFromTensor("pos_embed0", poss.[0])
    //         NamedOnnxValue.CreateFromTensor("pos_embed1", poss.[1])
    //         NamedOnnxValue.CreateFromTensor("pos_embed2", poss.[2])
    //         NamedOnnxValue.CreateFromTensor("pos_embed3", poss.[3])
    //         NamedOnnxValue.CreateFromTensor("tgt", DenseTensor<bool>(ReadOnlySpan [| 1; 4 |]))
    //         
    //         NamedOnnxValue.CreateFromTensor("encoded_text", encodedText)
    //         NamedOnnxValue.CreateFromTensor("text_token_mask", DenseTensor<bool>(ReadOnlySpan [| 1; 4; 4 |]))
    //     ]
    //     
    // exit 0
    Aardvark.Init()
    Aardium.init()
    let app = new OpenGlApplication()


    let file =
        if args.Length > 0 then Some args.[0]
        else Path.combine [__SOURCE_DIRECTORY__; ".."; "images"; "plants.png"] |> Some
        
    let run (ctx : DomContext) =
        App.start ctx (App.app file)


    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder.UseUrls("http://localhost:4321")
                    .UseSockets()
                    .Configure(fun b -> b.UseWebSockets().UseGiraffe (DomNode.toRoute app.Runtime run))
                    .ConfigureServices(fun s -> s.AddGiraffe() |> ignore)
                    |> ignore
        )
        .Build()
        .Start()
    
    Aardium.run {
        width 1280
        height 720
        url "http://localhost:4321"
    }
    
    0
