namespace Counter.IOS

open System

open UIKit
open Foundation

[<Register("AppDelegate")>]
type AppDelegate() =
  inherit UIApplicationDelegate()

  override val Window = null with get, set

  override this.FinishedLaunching(app, options) = true
