﻿open System
open System.Windows
open Counter.WPF
open Counter.Core

[<STAThread>]
[<EntryPoint>]
let main argv = 
  let mainWindow = new MainWindow ()
  let application = new Application ()
  MainComponent.runInView mainWindow
  application.Run mainWindow