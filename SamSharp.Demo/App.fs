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

module App =
    let initial = CameraController.initial
    let sam = new Sam()
    
    let view (file : string) (env : Env<CameraController.Message>) (m : AdaptiveCameraModel) =
        
        let img = PixImageSharp.Create file
        let idx = sam.BuildIndex img
        let box = Box2d.FromCenterAndSize(V2d.Zero, V2d img.Size)
        env.Emit [ CameraController.SetSceneBounds box ]
        
        let mask = cval (Matrix<float32>(V2i.II))
        
        let maskTexture =
            mask |> AVal.map (fun m ->
                let img = PixImage<float32>(Col.Format.Gray, m.AsVolume())
                PixTexture2d(PixImageMipMap [| img :> PixImage |], TextureParams.empty) :> ITexture   
            )
        
        let mutable initial = true
        body {
            Style [
                Padding "0"
                Margin "0"
                Position "fixed"
                Width "100%"
                Height "100%"
            ]
            renderControl {
                
                Style [
                    Background "black"
                    Width "100%"
                    Height "100%"
                ]
                CameraController.attributes env (fun e -> true) (fun e -> true) (AVal.constant m)
                
                Sg.View (m.camera |> AVal.map (fun c -> CameraView.viewTrafo c.cameraView))
                Sg.Proj (m.camera |> AVal.map (fun c -> Frustum.projTrafo c.frustum))
                
                Sg.OnTap(fun e ->
                    let px = V2i(round (e.WorldPosition.XY - box.Min))
                    let px = V2i(px.X, img.Size.Y - px.Y - 1)
                    Log.line "%A" px
                    let m = idx.Query [ Query.Point(px, 1) ]
                    transact (fun () -> mask.Value <- m)
                )
                
                RenderControl.OnResize (fun e ->
                    env.Emit [ CameraController.Resize e.Size ]
                    if initial then
                        initial <- false
                        env.Emit [ CameraController.BestFit box ]   
                )
                
                RenderControl.OnRendered (fun e ->
                    if AVal.force m.needsAnimation then
                        env.Emit [ CameraController.Interpolate ]    
                )
                
                sg {
                    Sg.Shader {
                        DefaultSurfaces.trafo
                        Shader.texture
                    }
                    Sg.Uniform("DiffuseColorTexture", PixTexture2d(PixImageMipMap [| img |], TextureParams.mipmapped) :> ITexture |> AVal.constant)
                    Sg.Uniform("Mask", maskTexture)
                    
                    Sg.Trafo(Trafo3d.Scale(float img.Size.X / 2.0, float img.Size.Y / 2.0, 1.0))
                    Primitives.ScreenQuad 0.0
                }
            }
        }

    let app file =
        {
            initial = CameraController.initial
            update = fun _env model msg -> CameraController.update model msg
            view = view file
            unpersist = Unpersist.instance 
        }