#r @"PresentationCore"
#r @"PresentationFramework"
#r @"WindowsBase"
#r @"System.Xaml"
#r @"UIAutomationTypes"
#r @"packages\Rx-Core.2.2.5\lib\net45\System.Reactive.Core.dll"
#r @"packages\Rx-Interfaces.2.2.5\lib\net45\System.Reactive.Interfaces.dll"
#r @"packages\Rx-Linq.2.2.5\lib\net45\System.Reactive.Linq.dll"
//#r @"packages\FSharpx.TypeProviders.Xaml.1.8.41\lib\40\FSharpx.TypeProviders.Xaml.dll"

open System
open System.Windows          
open System.Windows.Controls  
open System.Windows.Media
open System.Windows.Shapes
open System.Reactive
open System.Reactive.Linq
//open FSharpx

/// This operator is similar to (|>). 
/// But, it returns argument as a return value.
/// Then you can chain functions which returns unit.
let ($) x f = f x ; x

type StackPanel with
  /// Helper function to compose a GUI
  member o.add x = o.Children.Add x |> ignore

/// This container is used by some controls to share a variable.
/// If the value is changed, it fires changed event.
/// Controls should have this instead of their own internal data
type SharedValue<'a when 'a : equality>(value:'a) =
  let mutable _value = value
  let changed = Event<'a>()
  member o.Get       = _value
  member o.Set value =
    let old = _value 
    _value <- value
    if old <> _value then _value |> changed.Trigger
  member o.Changed = changed.Publish
type share<'a when 'a : equality> = SharedValue<'a>

//
// user control declarations
//
/// Volume control , it shows a value and allows you to change it.
type Volume(title:string, range:int * int, value:share<int>) as this =
  inherit StackPanel(Orientation=Orientation.Horizontal)
  do Label(Content=title,Width=50.) |>this.add 
  let label  = Label(Content=value.Get,Width=50.) $ this.add
  let slider = Slider(Minimum=float(fst range), Maximum=float(snd range), TickFrequency=2., Width=127.) $ this.add
  let changedHandler value =
    label.Content <- string value
    slider.Value  <- float value
  do
    // specifying how to cooperate shared value and slider control
    slider.ValueChanged.Add(fun arg -> int arg.NewValue |> value.Set)
    value.Changed.Add changedHandler

    changedHandler value.Get // initialization

/// Volume control of a color
type ColorVolume (color:share<Color>) as this =
  inherit StackPanel(Orientation=Orientation.Vertical)
  // shared values for controls which represents ARGB of selected color
  let alpha = SharedValue(int color.Get.A)
  let red   = SharedValue(int color.Get.R)
  let green = SharedValue(int color.Get.G)
  let blue  = SharedValue(int color.Get.B)
  do
    // specifying how to calculate dependent shared values
    let argbChanged = alpha.Changed |> Observable.merge red.Changed |> Observable.merge green.Changed |> Observable.merge blue.Changed
    argbChanged.Add(fun _ ->
      color.Set(Color.FromArgb(byte alpha.Get,byte red.Get,byte green.Get,byte blue.Get))
      )
    color.Changed.Add(fun color ->
      alpha.Set (int color.A)
      red.Set   (int color.R)
      green.Set (int color.G)
      blue.Set  (int color.B)
      )
    // adding volume controls
    Volume("Alpha", (0,255), alpha) |> this.add
    Volume("Red"  , (0,255), red  ) |> this.add
    Volume("Green", (0,255), green) |> this.add
    Volume("Blue" , (0,255), blue ) |> this.add

[<RequireQualifiedAccess>]
type MyShapes = Rectangle | Ellipse
/// Shape container control which reacts when properties of a shape is changed.
type ShapeContainer(shapes:share<MyShapes>,width:share<int>,height:share<int>,color:share<Color>) as this =
  inherit Label(Width=250., Height=250.)
  let mutable shape = Ellipse() :> Shape
  let setWidth  width  = shape.Width  <- float width
  let setHeight height = shape.Height <- float height
  let setColor  color  = shape.Fill   <- SolidColorBrush(color)
  let initShape () =
    this.Content <- shape
    setWidth  width.Get
    setHeight height.Get
    setColor  color.Get
  let setShape du =
    match du with
      | MyShapes.Rectangle -> shape <- Rectangle()
      | MyShapes.Ellipse   -> shape <- Ellipse  ()
    initShape ()
  do
    // specifying cooperations with shared values and the shape
    width.Changed.Add  setWidth
    height.Changed.Add setHeight
    color.Changed.Add  setColor 
    shapes.Changed.Add setShape
    // initialization
    initShape ()

//
// compose controls
//
/// This StackPanel contains every controls in this program
let stackPanel = StackPanel(Orientation=Orientation.Vertical)

let width = SharedValue(120)
Volume("Width",(50, 240),width) |> stackPanel.add // add a volume to the StackPanel

let height = SharedValue(80)
Volume("Height",(50, 200),height) |> stackPanel.add // add a volume to the StackPanel

let color = SharedValue(Colors.Blue)
ColorVolume(color) |> stackPanel.add // add volumes to the StackPanel

let shapes = SharedValue(MyShapes.Ellipse)
let ellipseButton   = Button(Content="Ellipse")   $ stackPanel.add
let rectangleButton = Button(Content="Rectangle") $ stackPanel.add
ellipseButton.Click.Add(  fun _ -> shapes.Set MyShapes.Ellipse)   // add event handler to fire dependency calculation
rectangleButton.Click.Add(fun _ -> shapes.Set MyShapes.Rectangle)

// This is a shape control shown in the bottom of this program's window
ShapeContainer(shapes,width,height,color) |> stackPanel.add

// Make a window and show it
let dock = new DockPanel()
let menu = new Menu()
let file = new MenuItem(Header="_File")
let openFolder = new MenuItem(Header="Open folder")
openFolder.Click |> Observable.subscribe (fun e -> printfn "click")

file.Items.Add(openFolder)
file.Items.Add(new MenuItem(Header="Open folder"))
file.Items.Add(new MenuItem(Header="save"))
menu.Items.Add(file)
dock.Children.Add(menu)

let grid = new Grid()
grid.ColumnDefinitions.Add(new ColumnDefinition(Width=new GridLength(1.,GridUnitType.Star)))
grid.ColumnDefinitions.Add(new ColumnDefinition(Width=new GridLength(5.)))
grid.ColumnDefinitions.Add(new ColumnDefinition(Width=new GridLength(2.,GridUnitType.Star)))
dock.Children.Add(grid)
let tree = new TreeView()
type MenuItem = 
    {
        title:String
    }
let template = new HierarchicalDataTemplate(typeof<MenuItem>)
let labelFactory = new FrameworkElementFactory(typeof<TextBlock>);
template.ItemsSource = "Items"
//https://social.msdn.microsoft.com/Forums/vstudio/en-US/acc87765-618e-4afd-b695-df5144d904ac/hierarchicaldatatemplate-for-treeview-programmatically-c?forum=wpf


//template.DataType = "MenuItem"
tree.ItemTemplate = template
Grid.SetColumn(tree,0)
grid.Children.Add(tree)

let window = Window(Title="F# is fun!",Width=260., Height=420., Content = dock, Topmost = true)
window.Show()
