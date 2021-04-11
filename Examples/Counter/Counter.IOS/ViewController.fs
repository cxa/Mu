namespace Counter.IOS

open System

open Foundation
open UIKit

open Mu
open Counter.Core
open MainComponent

[<Register("ViewController")>]
type ViewController(handle: IntPtr) =
  inherit UIViewController(handle)

  [<Outlet>] member val countLabel: UILabel = null with get, set
  [<Outlet>] member val incrButton: UIButton = null with get, set
  [<Outlet>] member val decrButton: UIButton = null with get, set
  [<Outlet>] member val randButton: UIButton = null with get, set
  [<Outlet>] member val descLabel: UILabel = null with get, set

  override x.ViewDidLoad() =
    base.ViewDidLoad()
    MainComponent.runInView x

  interface IView<Model, Msg.T> with
    member x.BindModel model =
      <@ x.countLabel.Text <- model.NumberString
         x.randButton.SetTitle(model.ButtonTitle, UIControlState.Normal)
         x.descLabel.Text <- model.Message @>

    member x.BindMsg send =
      x.incrButton.TouchUpInside.Add(fun _ -> send Msg.Incr)
      x.decrButton.TouchUpInside.Add(fun _ -> send Msg.Decr)
      x.randButton.TouchUpInside.Add(fun _ -> send Msg.HandleRandomizing)
