open System
open System.Windows
open Counter.WPF

[<STAThread>]
[<EntryPoint>]
let main argv =
  let mainWindow = new MainWindow()
  let application = new Application()
  application.Run mainWindow
