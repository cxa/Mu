# Mu

Apply The Elm Architecture pattern on Xamarin stack, with another simple, unobtrusive way.

## The Elm Architecture (TEA)

[TEA](https://guide.elm-lang.org/architecture/) is all about Model, Update, and View, in **Mu**, it's specific to:

- Model — the state of an GUI component
- Update — a way to update your state
- View — a way to view your state as `UIView(Controller)`, `NSView(Controller)`, `Activity` and other platform view stacks that Xamarin supported

This is a simple pattern for architecting GUI components.

```fsharp
// Model
type Model = { ... }

// Update
type Event = Reset | ...

let update model event =
  match event with
  | Reset -> Update { model with ... }
  ...

// View
type ViewController (handle:IntPtr) =
  inherit UIViewController (handle)
  ...

  override x.ViewDidLoad () =
    base.ViewDidLoad ()
    Mu.run initModel update x

  interface Mu.IView<Model, Event> with
    member x.BindModel model binder = 
      binder.Bind <@ model.Field @> (fun fieldValue -> x.someLabel.Text <- fieldValue)
      ...

    member x.BindEvent emit =
      x.resetButton.TouchUpInside.Add (fun _ -> emit Reset)
      ...
```

That is really the essence of The Elm Architecture! With **Mu**, Model and Update are separated from view, this means that they can shared across platforms, to support more platforms, only need to make platform-specific view implementing `Mu.IView<'Model, 'Event>` interfaces.

### Model

Model should be a DTO(Data Transfer Object) only, immutable record is the best way to represent model.

### Update

```fsharp
type EmitEvent<'event> =
  'event -> unit

type SideEffects<'model, 'event> =
  'model -> EmitEvent<'event> -> unit

type Update<'model, 'event> =
  | Update of 'model
  | UpdateWithSideEffects of 'model * SideEffects<'model, 'event>
  | SideEffects of SideEffects<'model, 'event>
```

Not all updates are just model changes, side effects without or with model changing are common. **Mu** provides current model state and the event emitter when performing side effects.

### View

```fsharp
type IView<'model, 'event> =
  abstract BindModel: 'model -> Binder.I -> unit
  abstract BindEvent: EmitEvent<'event> -> unit
```

view is only an interface in **Mu**, this is the most unobtrusive way to introduce 3rd lib into your project. `BindModel` provides model and binder to sync model states to view elements, and `BindEvent` provides an event emitter to make user input possible.

## Examples

👉 [Examples](Examples)

## Usage

- Install from NuGet: [https://www.nuget.org/packages/com.realazy.Mu](https://www.nuget.org/packages/com.realazy.Mu)
- Add this project to your solution, or directly add `Mu.fs`, yes `Mu` is a single file project, less than 100 LOC! 🤯

## LICENSE

MIT

## Author

- Blog: [realazy.com](https://realazy.com) (Chinese)
- Github: [@cxa](https://github.com/cxa)
- Twitter: [@_cxa](https://twitter.com/_cxa) (Chinese mainly)
