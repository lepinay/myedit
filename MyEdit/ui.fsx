#r @"PresentationCore"
#r @"PresentationFramework"
#r @"WindowsBase"
#r @"System.Xaml"
#r @"UIAutomationTypes"
#r @"packages\Rx-Core.2.2.5\lib\net45\System.Reactive.Core.dll"
#r @"packages\Rx-Interfaces.2.2.5\lib\net45\System.Reactive.Interfaces.dll"
#r @"packages\Rx-Linq.2.2.5\lib\net45\System.Reactive.Linq.dll"
#r @"packages\FSharpx.TypeProviders.Xaml.1.8.41\lib\40\FSharpx.TypeProviders.Xaml.dll"

open System
open System.Windows          
open System.Windows.Controls  
open System.Windows.Media
open System.Windows.Shapes
open System.Reactive
open System.Reactive.Linq
open FSharpx
open System.Windows.Data


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

let grid cols (childrens:UIElement list) =
    let g = new Grid()
    for c in cols do g.ColumnDefinitions.Add(new ColumnDefinition(Width=c))
    for child in childrens do g.Children.Add(child) |> ignore
    g

let tree = new TreeView()
type MenuItem() =
    member val Title = "" with get, set
    member val Items:MenuItem list = [] with get, set 

let verticalAlignment a (gs:GridSplitter) = gs.VerticalAlignment <- a
let horizontalAlignment a (gs:GridSplitter) = gs.HorizontalAlignment <- a
let resizeDirection a (gs:GridSplitter) = gs.ResizeDirection <- a
let showsPreview a (gs:GridSplitter) = gs.ShowsPreview <- a
let width a (gs:GridSplitter) = gs.Width <- a
let column a (gs:GridSplitter) = Grid.SetColumn(gs,a)

//https://social.msdn.microsoft.com/Forums/vstudio/en-US/acc87765-618e-4afd-b695-df5144d904ac/hierarchicaldatatemplate-for-treeview-programmatically-c?forum=wpf    
let template = new HierarchicalDataTemplate(typeof<MenuItem>)
let labelFactory = new FrameworkElementFactory(typeof<TextBlock>);
labelFactory.SetBinding(TextBlock.TextProperty, new Binding("Title"));
template.ItemsSource <- new Binding("Items")
template.VisualTree <- labelFactory
tree.ItemTemplate <- template
tree.ItemsSource <- [MenuItem(Title="Hello :)", Items=[MenuItem(Title="World")])]

Grid.SetColumn(tree,0)



let splitter props = 
    let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Center,ResizeDirection=GridResizeDirection.Columns,ShowsPreview=true,Width=5.)
    for p in props do p gs
    gs




let window = Window(Title="F# is fun!",Width=260., Height=420., Topmost = true)
window.Content <- 
    dock  [lastChildFill true]
            [menu 
                [item "_File" []
                    [item "Open folder" [(fun e -> printfn "click")] []
                     item "Save" [] []]]
             grid [GridLength(1.,GridUnitType.Star)
                   GridLength(5.)
                   GridLength(2.,GridUnitType.Star)]
                   [tree
                    splitter [column 1 
                              verticalAlignment VerticalAlignment.Stretch
                              horizontalAlignment HorizontalAlignment.Center
                              resizeDirection GridResizeDirection.Columns 
                              showsPreview true;width 5.] ]]
window.Show()






