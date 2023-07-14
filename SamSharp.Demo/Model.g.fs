//9b93b108-a6c7-9bd5-83a8-0bd41cb1a156
//059b6a36-5530-a93e-a3f1-5e0aacb3abeb
#nowarn "49" // upper case patterns
#nowarn "66" // upcast is unncecessary
#nowarn "1337" // internal types
#nowarn "1182" // value is unused
namespace rec SamSharp.Demo

open System
open FSharp.Data.Adaptive
open Adaptify
open SamSharp.Demo
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "*")>]
type AdaptiveCenterAndScale(value : CenterAndScale) =
    let _center_ = FSharp.Data.Adaptive.cval(value.center)
    let _scale_ = FSharp.Data.Adaptive.cval(value.scale)
    let mutable __value = value
    let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (token : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
    static member Create(value : CenterAndScale) = AdaptiveCenterAndScale(value)
    static member Unpersist = Adaptify.Unpersist.create (fun (value : CenterAndScale) -> AdaptiveCenterAndScale(value)) (fun (adaptive : AdaptiveCenterAndScale) (value : CenterAndScale) -> adaptive.Update(value))
    member __.Update(value : CenterAndScale) =
        if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<CenterAndScale>.ShallowEquals(value, __value))) then
            __value <- value
            __adaptive.MarkOutdated()
            _center_.Value <- value.center
            _scale_.Value <- value.scale
    member __.Current = __adaptive
    member __.center = _center_ :> FSharp.Data.Adaptive.aval<Aardvark.Base.V2d>
    member __.scale = _scale_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.float>
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "*")>]
type AdaptiveCameraModel(value : CameraModel) =
    let mutable _needsAnimation_ = FSharp.Data.Adaptive.cval(value.needsAnimation)
    let mutable _bounds_ = FSharp.Data.Adaptive.cval(value.bounds)
    let _sceneBounds_ = FSharp.Data.Adaptive.cval(value.sceneBounds)
    let _minPixelsVisible_ = FSharp.Data.Adaptive.cval(value.minPixelsVisible)
    let _zoomSensitivity_ = FSharp.Data.Adaptive.cval(value.zoomSensitivity)
    let _speed_ = FSharp.Data.Adaptive.cval(value.speed)
    let _viewportSize_ = FSharp.Data.Adaptive.cval(value.viewportSize)
    let _panning_ = FSharp.Data.Adaptive.cval(value.panning)
    let _current_ = AdaptiveCenterAndScale(value.current)
    let _camera_ = FSharp.Data.Adaptive.cval(value.camera)
    let mutable __value = value
    let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (token : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
    static member Create(value : CameraModel) = AdaptiveCameraModel(value)
    static member Unpersist = Adaptify.Unpersist.create (fun (value : CameraModel) -> AdaptiveCameraModel(value)) (fun (adaptive : AdaptiveCameraModel) (value : CameraModel) -> adaptive.Update(value))
    member __.Update(value : CameraModel) =
        if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<CameraModel>.ShallowEquals(value, __value))) then
            __value <- value
            __adaptive.MarkOutdated()
            _needsAnimation_.Value <- value.needsAnimation
            _bounds_.Value <- value.bounds
            _sceneBounds_.Value <- value.sceneBounds
            _minPixelsVisible_.Value <- value.minPixelsVisible
            _zoomSensitivity_.Value <- value.zoomSensitivity
            _speed_.Value <- value.speed
            _viewportSize_.Value <- value.viewportSize
            _panning_.Value <- value.panning
            _current_.Update(value.current)
            _camera_.Value <- value.camera
    member __.Current = __adaptive
    member __.needsAnimation = _needsAnimation_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.bool>
    member __.bounds = _bounds_ :> FSharp.Data.Adaptive.aval<Aardvark.Base.Box2d>
    member __.sceneBounds = _sceneBounds_ :> FSharp.Data.Adaptive.aval<Aardvark.Base.Box2d>
    member __.minPixelsVisible = _minPixelsVisible_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.int>
    member __.zoomSensitivity = _zoomSensitivity_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.float>
    member __.speed = _speed_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.float>
    member __.viewportSize = _viewportSize_ :> FSharp.Data.Adaptive.aval<Aardvark.Base.V2i>
    member __.panning = _panning_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.bool>
    member __.current = _current_
    member __.target = __value.target
    member __.lastPos = __value.lastPos
    member __.lastTime = __value.lastTime
    member __.camera = _camera_ :> FSharp.Data.Adaptive.aval<Aardvark.Rendering.Camera>
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "*")>]
type AdaptiveModel(value : Model) =
    let _Camera_ = AdaptiveCameraModel(value.Camera)
    let _File_ = FSharp.Data.Adaptive.cval(value.File)
    let _Image_ = FSharp.Data.Adaptive.cval(value.Image)
    let _Index_ = FSharp.Data.Adaptive.cval(value.Index)
    let _Mask_ = FSharp.Data.Adaptive.cval(value.Mask)
    let mutable __value = value
    let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (token : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
    static member Create(value : Model) = AdaptiveModel(value)
    static member Unpersist = Adaptify.Unpersist.create (fun (value : Model) -> AdaptiveModel(value)) (fun (adaptive : AdaptiveModel) (value : Model) -> adaptive.Update(value))
    member __.Update(value : Model) =
        if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<Model>.ShallowEquals(value, __value))) then
            __value <- value
            __adaptive.MarkOutdated()
            _Camera_.Update(value.Camera)
            _File_.Value <- value.File
            _Image_.Value <- value.Image
            _Index_.Value <- value.Index
            _Mask_.Value <- value.Mask
    member __.Current = __adaptive
    member __.Camera = _Camera_
    member __.File = _File_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.option<Microsoft.FSharp.Core.string>>
    member __.Image = _Image_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.option<Aardvark.Base.PixImage>>
    member __.Index = _Index_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.option<SamSharp.SamIndex>>
    member __.Mask = _Mask_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.option<Aardvark.Base.Matrix<Microsoft.FSharp.Core.float32>>>

