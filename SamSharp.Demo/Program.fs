open Aardvark.Application.Slim
open Aardvark.Base
open Aardvark.Dom
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Giraffe
open Aardvark.Dom.Remote
open SamSharp.Demo
open Aardium


[<EntryPoint>]
let main args =
    Aardvark.Init()
    Aardium.init()
    let app = new OpenGlApplication()


    let file =
        if args.Length > 0 then args.[0]
        else Path.combine [__SOURCE_DIRECTORY__; ".."; "images"; "plants.png"]
        
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
        url "http://localhost:4321"
    }
    
    0
