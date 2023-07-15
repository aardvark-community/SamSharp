namespace SamSharp.Demo

open SamSharp
open Adaptify
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Dom

module Shader =
    open FShade
    let color =
        sampler2d {
            texture uniform?DiffuseColorTexture
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipLinear
        }
    let mask =
        sampler2d {
            texture uniform?Mask
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagLinear
        }
    
    let texture (v : Effects.Vertex) =
        fragment {
            let c = color.Sample(v.tc)
            let m = mask.Sample(v.tc).X * 0.6
            
            return V4d(lerp c.XYZ V3d.IOO m, 1.0)
        }

[<AutoOpen>]
module DomExtensions =
    open Aardvark.Dom.Extensions
    open Aardvark.Dom.Extensions.NodeBuilderHelpers
    open Aardvark.Dom.NodeBuilderHelpers
    
    type DropValue =
        | Text of mime : string * content : string
        | File of name : string * mime : string * data : byte[]
    
    module Dom =
        type OnDrop = OnDrop of (list<DropValue> -> unit)
    
    type DropOverlayState =
        {
            OnDropActions : list<list<DropValue> -> unit>
        }
    
    type DropOverlayBuilder() =
        inherit StatefulNodeLikeBuilder<DropOverlayState, DomNode, Aardvark.Dom.NodeBuilderHelpers.NodeBuilderState>()
        
        member x.Yield(Dom.OnDrop action) =
            fun (s : DropOverlayState) ->
                { s with OnDropActions = action :: s.OnDropActions }, NodeLikeBuilderState.Empty
               
        member x.Yield(text : string) =
            x.Yield(DomNode.Text (AVal.constant text))
            
        member x.Yield(text : aval<string>) =
            x.Yield(DomNode.Text text)
                
        override x.Run(run : DropOverlayState -> DropOverlayState * NodeLikeBuilderState<DomNode>) =
            let state, res = run { OnDropActions = [] }
            
            let id = RandomElementId()
            
            let overlay = 
                div {
                    Id id
                    Style [
                        PointerEvents "none"
                        Background "rgba(30,30,30,0.7)"
                        Width "100%"
                        Height "100%"
                        Top "0px"
                        Left "0px"
                        Position "absolute"
                        ZIndex 10
                        Display "none"
                        FontFamily "monospace"
                        JustifyContent "center"
                        AlignItems "center"
                    ]
                    
                    res.attributes.ToAMap()
                    res.children.ToAList()
                }
            
            let onDrop (things : list<DropValue>) =
                for action in state.OnDropActions do
                    action things
                    
            
            let rx = System.Text.RegularExpressions.Regex @"^data:(?<mime>[^;]+);base64,"
            
            let atts =
                AttributeTable [
                    Dom.OnBoot(
                        (fun c -> 
                            task {
                                while true do
                                    let! msg = c.Receive()
                                    match msg with
                                    | ChannelMessage.Text str ->
                                        try
                                            let r = System.Text.Json.JsonDocument.Parse str
                                            
                                            let o = r.RootElement
                                            
                                            let result = System.Collections.Generic.List()
                                            for i in 0 .. o.GetArrayLength() - 1 do
                                                let e = o.[i]
                                                let typ = e.GetProperty("Type").GetString()
                                                let data = e.GetProperty("Data").GetString()
                                                
                                                let m = rx.Match data
                                                if m.Success then
                                                    let mime = m.Groups.[1].Value
                                                    let data = System.Convert.FromBase64CharArray(data.ToCharArray(), m.Length, data.Length - m.Length)
                                                    result.Add(File(typ, mime, data))
                                                else
                                                    result.Add(Text(typ, data))
                                            
                                            onDrop (CSharpList.toList result)
                                        with e ->
                                            onDrop []
                                            printfn "parse error: %A" e
                                    | _ ->
                                        ()
                            }
                        ),
                        fun c ->
                            String.concat "\n" [
                                $"function display(v) {{ document.getElementById('{id}').style.display = v; }}"
                                $"__THIS__.addEventListener('dragenter', function(e) {{ e.preventDefault(); display('flex'); }});"
                                $"__THIS__.addEventListener('dragleave', function(e) {{ e.preventDefault(); display('none'); }});"
                                $"__THIS__.addEventListener('dragover', function(e) {{ e.preventDefault(); }});"
                                
                                "function readFileAsync(name, file) {"
                                "   return new Promise((resolve, error) => {"
                                "       let reader = new FileReader();"
                                "       reader.onload = function(e) {{"
                                "           resolve({ Type: name, Data: e.target.result });"
                                "       }};"
                                "       reader.onerror = function(e) {{"
                                "           error(e);"
                                "       }};"
                                "       reader.readAsDataURL(file);"
                                "   });"
                                "}"
                                
                                $"function handleDrop(data) {{"
                                $"    const items = Array.from(data.items).map((i) => {{"
                                $"      if(i.kind === 'file') {{"
                                $"        const file = i.getAsFile();"
                                $"        return readFileAsync(file.name, file);"
                                $"      }}"
                                $"      else {{"
                                $"        const typ = i.type;"
                                $"        return new Promise((resolve, err) => i.getAsString((value) => resolve({{ Type: typ, Data: value }})));"
                                $"      }}"
                                $"    }});"
                                $"    Promise.all(items).then((values) => {{"
                                $"        {c}.send(JSON.stringify(values));"
                                $"    }});"
                                $"}}"
                                
                                $"__THIS__.addEventListener('drop', function(e) {{"
                                $"  display('none');"
                                $"  handleDrop(e.dataTransfer);"
                                $"  e.preventDefault();"
                                $"}});"
                                
                                $"__THIS__.addEventListener('paste', function(e) {{"
                                $"  display('none');"
                                $"  handleDrop(e.clipboardData);"
                                $"  e.preventDefault();"
                                $"}});"
                            ]
                    )
                ]
            
            let cs =
                NodeList [overlay]
            
            { attributes = atts; children = cs } 
        
    
    
    type Dom with
        static member DropOverlay = DropOverlayBuilder()

type Message =
    | CameraMessage of CameraController.Message
    | SetFile of string
    | SetIndex of PixImage * SamIndex
    | SetMask of Matrix<float32>
    | Clear

module App =
    
    let sam = new Sam()
    
    
    let initial =
        {
            Camera = CameraController.initial
            File = None
            Image = None
            Index = None
            Mask = None
        }
        
        
    let update (env : Env<Message>) (model : Model) (msg : Message) =
        match msg with
        | CameraMessage message -> { model with Camera = CameraController.update model.Camera message }
        | SetFile s ->
            env.Start "let l = document.getElementById('loader'); if(l) { l.style.display = 'flex'; }"
            { model with File = Some s; Index = None; Mask = None }
        | SetIndex (img, samIndex) ->
            env.Start "let l = document.getElementById('loader'); if(l) { l.style.display = 'none'; }"
            let box = Box2d.FromCenterAndSize(V2d.Zero, V2d img.Size)
            let cam = CameraController.update model.Camera (CameraController.SetSceneBounds box)
            let cam = CameraController.update cam (CameraController.BestFit box)
            { model with
                Index = Some samIndex
                Image = Some img
                Mask = None
                Camera = cam
            }
        | SetMask m -> { model with Mask = Some m }
        | Clear -> { model with File = None; Index = None; Mask = None }
        
    let view (file : string) (env : Env<Message>) (m : AdaptiveModel) =
        
        let texture = 
            m.Image |> AVal.map (function
                | Some img -> PixTexture2d(PixImageMipMap [| img :> PixImage |], TextureParams.mipmapped) :> ITexture
                | None -> NullTexture() :> ITexture
            )
            
        let maskTexture =
            m.Mask |> AVal.map (function
                | Some m ->
                    let img = PixImage<float32>(Col.Format.Gray, m.AsVolume())
                    PixTexture2d(PixImageMipMap [| img :> PixImage |], TextureParams.empty) :> ITexture
                | None ->
                    NullTexture() :> ITexture
            )
        
        body {
            Style [
                Padding "0"
                Margin "0"
                Position "fixed"
                Width "100%"
                Height "100%"
            ]
            
            let startLoad (name : string) (data : byte[]) =
                env.Emit [SetFile name] 
                
                task {
                    do! Async.SwitchToThreadPool()
                    try
                        use ms = new System.IO.MemoryStream(data)
                        let img = PixImageSharp.Create ms
                        let idx = sam.BuildIndex img
                        env.Emit [SetIndex(img, idx)]
                    with _ ->
                        env.Emit [Clear]
                } |> ignore
            
            
            Dom.DropOverlay {
                Style [
                    Color "white"
                    FontSize "80px"
                    FontFamily "monospace"
                ]
                
                Dom.OnDrop (fun things ->
                    let data =
                        things |> List.tryPick (function
                            | File(name, mime, data) when mime.StartsWith "image/" ->
                                Some (name, data)
                            | Text("text/plain", str) ->
                                if System.IO.File.Exists str then
                                    try Some (System.IO.Path.GetFileName str, File.readAllBytes str)
                                    with _ -> None
                                else
                                    None
                            | _ ->
                                None   
                        )
                    match data with
                    | Some (name, data) -> startLoad name data
                    | None -> ()
                )
                
                "+"
            }
            
            div {
                Id "loader"
                Style [
                    UserSelect "none"
                    Background "rgb(30 30 30)"
                    Width "100%"
                    Height "100%"
                    Position "fixed"
                    Top "0px"
                    Left "0px"
                    ZIndex 20
                    Display "flex"
                    Color "white"
                    FontSize "40px"
                    FontFamily "monospace"
                    JustifyContent "center"
                    AlignItems "center"
                ]
                
                let content =
                    m.File |> AVal.map (function
                        | None -> "Drop an Image here to start"
                        | Some f -> sprintf "Loading '%s' ..." (System.IO.Path.GetFileName f))
                
                content
                
                
            }
            
            renderControl {
                
                Style [
                    Background "rgb(30 30 30)"
                    Width "100%"
                    Height "100%"
                ]
                CameraController.attributes (Env.map CameraMessage env) (fun e -> true) (fun e -> true) (AVal.constant m.Camera)
                
                Sg.View (m.Camera.camera |> AVal.map (fun c -> CameraView.viewTrafo c.cameraView))
                Sg.Proj (m.Camera.camera |> AVal.map (fun c -> Frustum.projTrafo c.frustum))
                
                Sg.OnTap(fun e ->
                    match AVal.force m.Index, AVal.force m.Image with
                    | Some idx, Some img ->
                        let box = Box2d.FromCenterAndSize(V2d.Zero, V2d img.Size)
                        let px = V2i(round (e.WorldPosition.XY - box.Min))
                        let px = V2i(px.X, img.Size.Y - px.Y - 1)
                        let m = idx.Query [ Query.Point(px, 1) ]
                        env.Emit [SetMask m]
                    | _ ->
                        ()
                )
                RenderControl.OnReady (fun _ ->
                    if System.IO.File.Exists file then
                        startLoad (System.IO.Path.GetFileName file) (System.IO.File.ReadAllBytes file)
                )
                RenderControl.OnResize (fun e ->
                    env.Emit [ CameraMessage (CameraController.Resize e.Size) ]
                )
                
                RenderControl.OnRendered (fun e ->
                    if AVal.force m.Camera.needsAnimation then
                        env.Emit [ CameraMessage(CameraController.Interpolate) ]    
                )
                
                sg {
                    Sg.Shader {
                        DefaultSurfaces.trafo
                        Shader.texture
                    }
                    Sg.Uniform("DiffuseColorTexture", texture)
                    Sg.Uniform("Mask", maskTexture)
                    
                    let trafo =
                        m.Image |> AVal.map (function
                            | Some img -> Trafo3d.Scale(float img.Size.X / 2.0, float img.Size.Y / 2.0, 1.0)
                            | None -> Trafo3d.Identity
                        )
                    Sg.Trafo(trafo)
                    Primitives.ScreenQuad 0.0
                }
            }
            
            div {
                Style [
                    Position "fixed"; Top "10px"; Left "10px"
                    Padding "10px"
                    Background "rgba(0,0,0,0.7)"; BackdropFilter "blur(10px)"
                    FontFamily "monospace"
                    FontSize "1.2em"
                    Color "white"
                ]
                
                ul {
                    li { "pan with the left mouse button" }
                    li { "zoom using the scroll-wheel" }
                    li { "drop a file onto the program to load it" }
                    li { "left-click to start segmentation" }
                }
            }
            
        }

    let app file =
        {
            initial = initial
            update = update
            view = view file
            unpersist = Unpersist.instance 
        }