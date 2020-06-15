namespace Counter.Mac

open System

open Foundation
open AppKit

open Mu
open Counter.Core
open MainComponent

[<Register ("ViewController")>]
type ViewController (handle:IntPtr) =
  inherit NSViewController (handle)

  [<Outlet>] member val countLabel:NSTextField = null with get, set
  [<Outlet>] member val incrButton:NSButton = null with get, set
  [<Outlet>] member val decrButton:NSButton = null with get, set
  [<Outlet>] member val randomButton:NSButton = null with get, set
  [<Outlet>] member val descLabel:NSTextField = null with get, set

  override x.ViewDidLoad () =
    base.ViewDidLoad ()
    MainComponent.runInView x

  interface IView<Model, Action.T> with
    member x.BindModel model =
      <@
      x.countLabel.StringValue <- sprintf "%d" model.Number
      x.randomButton.Title <- model.ButtonTitle
      x.descLabel.StringValue <- model.Message
      @>

    member x.BindAction send =
      x.incrButton.Activated.Add (fun _ -> send Action.Incr)
      x.decrButton.Activated.Add (fun _ -> send Action.Decr)
      x.randomButton.Activated.Add (fun _ -> send Action.HandleRandomizing)
