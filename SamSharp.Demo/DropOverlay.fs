namespace Aardvark.Dom

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Dom.Extensions
open Aardvark.Dom.Extensions.NodeBuilderHelpers
open Aardvark.Dom.NodeBuilderHelpers
open Aardvark.Dom

[<RequireQualifiedAccess>]
type DropValue =
    | Text of mime : string * content : string
    | File of name : string * mime : string * data : byte[]

module DropOverlay =
    type OnDrop = OnDrop of (list<DropValue> -> unit)

    [<CompilerMessage("internal", 3180, IsHidden = true)>]
    type DropOverlayState =
        {
            OnDropActions : list<list<DropValue> -> unit>
        }
    
    [<CompilerMessage("internal", 3180, IsHidden = true)>]
    type DropOverlayBuilder() =
        inherit StatefulNodeLikeBuilder<DropOverlayState, DomNode, Aardvark.Dom.NodeBuilderHelpers.NodeBuilderState>()
        
        member x.Yield(OnDrop action) =
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
                                                    result.Add(DropValue.File(typ, mime, data))
                                                else
                                                    result.Add(DropValue.Text(typ, data))
                                            
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
    
[<AutoOpen>]
module DropOverlayExtensions =
    let dropOverlay = DropOverlay.DropOverlayBuilder()

    type Dom with
        static member inline DropOverlay = dropOverlay