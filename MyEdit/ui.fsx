#r @"PresentationCore"
#r @"PresentationFramework"
#r @"WindowsBase"
#r @"System.Xaml"
#r @"UIAutomationTypes"
#r @"packages\Rx-Core.2.2.5\lib\net45\System.Reactive.Core.dll"
#r @"packages\Rx-Interfaces.2.2.5\lib\net45\System.Reactive.Interfaces.dll"
#r @"packages\Rx-Linq.2.2.5\lib\net45\System.Reactive.Linq.dll"
#r @"packages\FSharpx.TypeProviders.Xaml.1.8.41\lib\40\FSharpx.TypeProviders.Xaml.dll"
#r @"packages\AvalonEdit.5.0.2\lib\Net40\ICSharpCode.AvalonEdit.dll"
#r @"packages\Simple.Wpf.Terminal.1.33.0.0\lib\net40\Simple.Wpf.Terminal.dll"
#r @"packages\Rx-PlatformServices.2.2.5\lib\net45\System.Reactive.PlatformServices.dll"
#r @"packages\reactiveui-core.6.4.0.1\lib\Net45\ReactiveUI.dll"
#r @"packages\Splat.1.6.0\lib\Net45\Splat.dll"

open System
open System.Windows          
open System.Windows.Controls  
open System.Windows.Media
open System.Windows.Shapes
open System.Reactive
open System.Reactive.Linq
open FSharpx
open System.Windows.Data
open ICSharpCode.AvalonEdit
open Simple.Wpf.Terminal
//open ReactiveUI

//#region helpers
let lastChildFill b (dock:DockPanel) = dock.LastChildFill <- b

let dock props (content:UIElement list) = 
    let d = new DockPanel()
    for p in props do p d
    for c in content do d.Children.Add(c) |> ignore
    d

let item name clicks (childrens:MenuItem list) =
    let i = new Controls.MenuItem(Header=name)
    for c in childrens do i.Items.Add(c) |> ignore
    for cl in clicks do
        i.Click |> Observable.subscribe cl |> ignore
    i

let menu items =
    let m = new Menu()
    for i in items do m.Items.Add(i) |> ignore
    DockPanel.SetDock(m,Dock.Top)
    m

let rows rows (grid:Grid) = for row in rows do grid.RowDefinitions.Add(new RowDefinition(Height=row))
let cols cols (grid:Grid) = for col in cols do grid.ColumnDefinitions.Add(new ColumnDefinition(Width=col))

let grid props (childrens:UIElement list) =
    let g = new Grid()
    for p in props do p g
    for child in childrens do g.Children.Add(child) |> ignore
    g

let tree props = 
    let t = new TreeView()
    for p in props do p t
    t

let splitter props = 
    let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Center,ResizeDirection=GridResizeDirection.Columns,ShowsPreview=true,Width=5.)
    for p in props do p gs
    gs

let itemTemplate template (tree:TreeView) = tree.ItemTemplate <- template
let itemSource source (tree:TreeView) = tree.ItemsSource <- source

let window c = 
    let w = Window(Title="F# is fun!",Width=260., Height=420., Topmost = true)
    w.Content <- c
    w

let show (w:Window) = w.Show()
let verticalAlignment a (gs:GridSplitter) = gs.VerticalAlignment <- a
let horizontalAlignment a (gs:GridSplitter) = gs.HorizontalAlignment <- a
let resizeDirection a (gs:GridSplitter) = gs.ResizeDirection <- a
let showsPreview a (gs:GridSplitter) = gs.ShowsPreview <- a
let width a (gs:GridSplitter) = gs.Width <- a
let height a (gs:GridSplitter) = gs.Height <- a
let column a (gs:UIElement) = Grid.SetColumn(gs,a)
let row a (gs:UIElement) = Grid.SetRow(gs,a)

//#endregion

type MenuItem() =
    member val Title = "" with get, set
    member val Items:MenuItem list = [] with get, set 



//https://social.msdn.microsoft.com/Forums/vstudio/en-US/acc87765-618e-4afd-b695-df5144d904ac/hierarchicaldatatemplate-for-treeview-programmatically-c?forum=wpf    
let template = new HierarchicalDataTemplate(typeof<MenuItem>)
let labelFactory = new FrameworkElementFactory(typeof<TextBlock>);
labelFactory.SetBinding(TextBlock.TextProperty, new Binding("Title"));
template.ItemsSource <- new Binding("Items")
template.VisualTree <- labelFactory

let fontFamily s (t:Terminal) = t.FontFamily <- FontFamily(s)
let terminal props items = 
    let t = new Terminal()
    for p in props do p t
    t.ItemsSource <- items
    t

let items = new ReactiveUI.ReactiveList<string>(["Hello"])

let tab props = 
    let t = new TabControl()
    let ti = new TabItem()
    let tb = new TextBlock()
    tb.Text <- "Plop" 
    ti.Content <- tb
    ti.Header <- "zooo"
    t.Items.Add(ti) |> ignore
    for p in props do p t
    t

dock 
    [lastChildFill true]
    [menu
        [item "_File" []
            [item "Open folder" [(fun e -> printfn "click")] []
             item "Save" [] []]]
     grid [cols [GridLength(1.,GridUnitType.Star);GridLength(5.);GridLength(2.,GridUnitType.Star)]]
        [
            tree [column 0;itemTemplate template;itemSource [MenuItem(Title="Hello :)", Items=[MenuItem(Title="World")])] ]
            splitter [column 1;verticalAlignment VerticalAlignment.Stretch;horizontalAlignment HorizontalAlignment.Center;resizeDirection GridResizeDirection.Columns;showsPreview true;width 5.]
            grid [column 2;rows [GridLength(1.,GridUnitType.Star);GridLength(5.);GridLength(1.,GridUnitType.Star)]; cols [GridLength(1.,GridUnitType.Star)]] 
                [
                    tab [row 0]
                    splitter [row 1;verticalAlignment VerticalAlignment.Center;horizontalAlignment HorizontalAlignment.Stretch;resizeDirection GridResizeDirection.Rows;showsPreview true;height 5.]
                    terminal [row 2;column 0;fontFamily "Consolas"] items
                ]
        ]
    ]
|> window
|> show







