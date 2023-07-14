open System
open Aardvark.Base
open SamSharp

        
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
            Point(V2i(610, 400), 1)
        ]
    Log.stop()
    
    Log.startTimed "save output"
    let result =
        let res = image.ToPixImage<byte>().Copy()
        res.GetMatrix<C4b>().SetMap2 (res.GetMatrix<C4b>(), mat, fun old value ->
            lerp old C4b.Red (0.7 * float value)    
        ) |> ignore
        res
    
    let outputPath = Path.combine [Environment.GetFolderPath Environment.SpecialFolder.Desktop; "segment.png"]
    result.SaveImageSharp outputPath
    Log.stop()
    0