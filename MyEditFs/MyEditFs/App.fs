module main

open System
open FsXaml

open System
open System.Windows          
open System.Linq
open System.Windows.Controls  
open System.Reactive.Linq
open ICSharpCode.AvalonEdit
open Simple.Wpf.Terminal
open System.Xml
open ICSharpCode.AvalonEdit.Highlighting
open ICSharpCode.AvalonEdit.Document
open System.Windows.Media
open MyEdit.Powershell
open System.Management.Automation.Runspaces
open System.Management.Automation
open System.Threading.Tasks
open System.Reactive.Concurrency
open System.Windows.Input
open ICSharpCode.AvalonEdit.Folding
open System.Diagnostics


//#region helpers
type Column = GridLength
type Row = GridLength
type Title = String
type SplitterDirection = Horizontal | Vertical


type Command =
    | BrowseFile
    | BrowseFolder
    | SaveFile
    | OpenFile of string
    | TextChanged of TextDocument
    | CommandOutput of string
    | DocSelected of TextDocument

type Element = 
    | Docked of Element*Dock
    | Column of Element*int
    | Row of Element*int
    | Dock of Element list
    | Menu of Element list
    | MenuItem of Title*Element list*Command list*String
    | Grid of Column list*Row list*Element list
    | Splitter of SplitterDirection
    | Terminal
    | Tree of Element list
    | TreeItem of Title*Element list
    | Editor of TextDocument
    | TabItem of (String*Element*Command)
    | TextArea of string
    | Tab of Element list
    | Scroll of Element

type Directory = 
    | None
    | Directory of string*Directory list*string list

type EditorState = {
    openFiles:(string*TextDocument) list
    current:TextDocument
    watches : (string) list
    consoleOutput : string
    currentFolder: Directory
}


let messages = new Reactive.Subjects.Subject<Command>()
let debug s = messages.OnNext <| CommandOutput s

let openFile () = 
    let dlg = new Microsoft.Win32.OpenFileDialog();
    dlg.DefaultExt <- ".cs";
    dlg.Filter <- "All Files (*.*)|*.*|CSharp Files (*.cs)|*.cs|Haskell Files (*.hs)|*.hs|FSharp Files (*.fs)|*.fs";
    let result = dlg.ShowDialog();
    if result.HasValue && result.Value then
        messages.OnNext(OpenFile dlg.FileName)
    ()

let openFolder () =     
    debug "open a folder"
    ()

let saveFile () = 
    debug "save a file"
    ()

// We could optimize this by diffing the state to the previous state and generate only the updated dom
// like we already do for the Dom to WPF transformation
let ui (state:EditorState) = 
    let tabs = state.openFiles |> List.map (fun (t,p) -> TabItem (IO.Path.GetFileName(t),Editor p, DocSelected p) )
    let rec makeTree = function
        | None -> []
        | Directory (p, folders, files) -> 
            let filest = List.map( fun f -> TreeItem(f,[])) files
            let folderst = List.map makeTree folders |> List.concat
            [TreeItem(p,  folderst @ filest )]

    let tree = Tree <| makeTree state.currentFolder
    Dock [Docked(Menu [MenuItem ("File",
                        [
                        MenuItem ("Open file",[], [BrowseFile], "" )
                        MenuItem ("Open folder",[], [BrowseFolder], "")
                        MenuItem ("Save",[], [SaveFile], "Ctrl+S")], [], "")],Dock.Top)
          Grid ([GridLength(1.,GridUnitType.Star);GridLength(5.);GridLength(2.,GridUnitType.Star)],[],[
                    Column(tree,0)
                    Column(Splitter Vertical,1)
                    Column(
                        Grid ([GridLength(1.,GridUnitType.Star)],
                              [GridLength(2.,GridUnitType.Star);GridLength(5.);GridLength(1.,GridUnitType.Star)],
                            [
                                Row(Tab tabs,0)
                                Row(Splitter Horizontal,1)
                                Row(Scroll(TextArea state.consoleOutput),2)
                            ]),2)
                ])
    ]


let rec render ui : UIElement = 
    let bgColor = new SolidColorBrush(Color.FromRgb (byte 39,byte 40,byte 34))
    let fgColor = new SolidColorBrush(Color.FromRgb (byte 248,byte 248,byte 242))

    match ui with
        | Dock xs -> 
            let d = new DockPanel(LastChildFill=true)
            for x in xs do d.Children.Add (render x) |> ignore
            d :> UIElement
        | Terminal -> new Terminal() :> UIElement
        | Tab xs -> 
            let d = new TabControl()            
            for x in xs do d.Items.Add (render x) |> ignore
            d.SelectionChanged |> Observable.subscribe(fun e -> 
                let item = d.SelectedItem :?> TabItem
                if item <> null then
                    let tag = item.Tag :?> Command
                    messages.OnNext tag |> ignore) |> ignore
            d :> UIElement
        | TabItem (title,e,com) ->
            let ti = new TabItem()
            ti.Content <- render e
            ti.Header <- title
            ti.IsSelected <- true
            ti.Tag <- com
            ti :> UIElement
        | Menu xs ->
            let m = new Menu()
            for x in xs do m.Items.Add (render x) |> ignore
            m :> UIElement
        | MenuItem (title,xs,actions,gestureText) -> 
            let mi = Controls.MenuItem(Header=title) 
            mi.InputGestureText <- gestureText
            for x in xs do mi.Items.Add(render x) |> ignore
            match actions with 
                | [BrowseFile] -> mi.Click |> Observable.subscribe(fun e -> openFile()) |> ignore
                | [SaveFile] -> mi.Click |> Observable.subscribe(fun e -> messages.OnNext(SaveFile)) |> ignore
                | other -> ()
            mi :> UIElement
        | Grid (cols,rows,xs) ->
            let g = new Grid()
            for row in rows do g.RowDefinitions.Add(new RowDefinition(Height=row))
            for col in cols do g.ColumnDefinitions.Add(new ColumnDefinition(Width=col))
            for x in xs do g.Children.Add(render x) |> ignore
            g :> UIElement
        | Docked (e,d) ->
            let elt = render e
            DockPanel.SetDock(elt,d)
            elt
        | Column (e,d) ->
            let elt = render e
            Grid.SetColumn(elt,d)
            elt
        | Row (e,d) ->
            let elt = render e
            Grid.SetRow(elt,d)
            elt
        | Splitter Vertical ->
            let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Center,ResizeDirection=GridResizeDirection.Columns,ShowsPreview=true,Width=5.)
            gs :> UIElement
        | Splitter Horizontal ->
            let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Stretch,ResizeDirection=GridResizeDirection.Rows,ShowsPreview=true,Height=5.)
            gs :> UIElement
        | Tree xs ->
            let t = new TreeView()
            for x in xs do t.Items.Add(render x) |> ignore
            t :> UIElement
        | TreeItem (title,xs) ->
            let ti = new TreeViewItem()
            ti.Header <- title
            for x in xs do ti.Items.Add(render x) |> ignore
            ti :> UIElement
        | Editor doc ->
            let editor = new TextEditor();
            let host = new System.Windows.Forms.Integration.WindowsFormsHost()
            let editor2 = new ScintillaNET.Scintilla()
            host.Child <- editor2

            editor2.Text <- doc.Text
            editor2.Font <- new Drawing.Font("Consolas",float32 10)
            editor2.Margins.[0].Width <- 20
            // https://scintillanet.codeplex.com/wikipage?title=HowToSyntax&referringTitle=Documentation
            editor2.ConfigurationManager.Language <- "haskell"
            editor2.ConfigurationManager.Cus <- "haskell"
            editor2.ConfigurationManager.Configure()

            editor.SyntaxHighlighting <- HighlightingManager.Instance.GetDefinitionByExtension(IO.Path.GetExtension(doc.FileName));
            editor.Document <- doc;
            editor.FontFamily <- FontFamily("Consolas")
            editor.TextChanged |> Observable.subscribe(fun e -> messages.OnNext(TextChanged doc)  ) |> ignore
            editor.ShowLineNumbers <- true
            editor.Background <- bgColor
            editor.Foreground <- fgColor
            editor.Options.ConvertTabsToSpaces <- true
            editor.Options.EnableHyperlinks <- false
            editor.Options.ShowColumnRuler <- true

            let foldingManager = FoldingManager.Install(editor.TextArea);
            let foldingStrategy = new XmlFoldingStrategy();
            foldingStrategy.UpdateFoldings(foldingManager, editor.Document);

            editor.Options.EnableRectangularSelection <- true

            host :> UIElement
        | TextArea s -> 
            let tb = new TextBox(Background = bgColor, Foreground = fgColor)
            tb.Text <- s
            tb.FontFamily <- FontFamily("Consolas")
            tb :> UIElement
        | Scroll e ->
            let scroll = new ScrollViewer()
            scroll.Content <- render e
            scroll :> UIElement
        | other -> failwith "not handled"

let collToList (coll:UIElementCollection) : UIElement list =
    seq { for c in coll -> c } |> Seq.toList

let itemsToList (coll:ItemCollection) =
    seq { for c in coll -> c :?> UIElement } |> Seq.toList

let rec resolve (prev:Element list) (curr:Element list) (screen:UIElement list) : UIElement list = 
    if prev = curr then screen 
    else 
        match (prev,curr,screen) with
            | (x::xs,y::ys,z::zs) when x = y -> z::resolve xs ys zs
            | ((TabItem (ta,ea,coma))::xs,(TabItem (tb,eb,comb))::ys,z::zs) when ea = eb -> 
                let ti = z :?> TabItem
                ti.Header <- tb
                ti.IsSelected <- true
                z::resolve xs ys zs
            | ((Tab a)::xs,(Tab b)::ys,z::zs) -> 
                let tab = z :?> TabControl     
                let childrens = (itemsToList tab.Items)
                tab.Items.Clear()
                for c in resolve a b childrens do 
                    tab.Items.Add(c) |> ignore
                (tab :> UIElement)::resolve xs ys zs
            | ((Dock a)::xs,(Dock b)::ys,z::zs) -> 
                let dock = z :?> DockPanel     
                let childrens = (collToList dock.Children)
                dock.Children.Clear()
                for c in resolve a b childrens do dock.Children.Add(c) |> ignore
                (dock :> UIElement)::resolve xs ys zs
            | ((Column (a,pa))::xs,(Column (b,pb))::ys,z::zs) when pa = pb -> resolve [a] [b] [z] @ resolve xs ys zs
            | ((Row (a,pa))::xs,(Row (b,pb))::ys,z::zs) when pa = pb -> resolve [a] [b] [z] @ resolve xs ys zs
            | ((Grid (acols,arows,a))::xs,(Grid (bcols,brows,b))::ys,z::zs) when acols = bcols && arows = brows -> 
                let grid = z :?> Grid     
                let childrens = (collToList grid.Children)
                grid.Children.Clear()
                for c in resolve a b childrens do grid.Children.Add(c) |> ignore
                (grid :> UIElement)::resolve xs ys zs
            | ((Editor tda)::xs,(Editor tdb)::ys,z::zs) when tda = tdb -> 
                z::resolve xs ys zs
            | ((Scroll a)::xs,(Scroll b)::ys,z::zs) -> 
                let scroll = z :?> ScrollViewer
                scroll.Content <- List.head <| resolve [a] [b] [scroll.Content :?> UIElement]
                scroll.ScrollToBottom()
                (scroll:>UIElement)::resolve xs ys zs
            | ((TextArea a)::xs,(TextArea b)::ys,z::zs)  -> 
                let tb = z :?> TextBox
                tb.AppendText("\n"+b)
                z::resolve xs ys zs
            | ([],y::ys,[]) -> (render y)::resolve [] ys []
            | ([],[],[]) -> []
            | (_,y::ys,_) -> 
                failwith <| sprintf "unable to reuse from %A" y
                (render y)::resolve [] ys []
            | other -> failwith <| sprintf "not handled:\n%A" other

// elm-make main.elm --yes
let intialState = {
    openFiles=[]
    watches=[("elm-make %currentpath% --yes")]
    consoleOutput=""
    current=null
    currentFolder=Directory ("Code", [Directory ("Src", [], ["file1.test";"file2.test"]) ], ["file1";"file2"]) 
    }


let myHost = new MyHost(fun s -> 
    Application.Current.Dispatcher.InvokeAsync(
        fun () ->
            debug s
            messages.OnNext <| CommandOutput s) |> ignore
    )
let myRunSpace = RunspaceFactory.CreateRunspace(myHost);
myRunSpace.Open();

let run (script:string) = 
    Task.Run 
        (fun () ->
//            use powershell = PowerShell.Create();
//            powershell.Runspace <- myRunSpace;
//            powershell.AddScript(script) |> ignore
//            powershell.AddCommand("out-default") |> ignore
//            powershell.Commands.Commands.[0].MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);
//            powershell.Invoke()
              let pi = ProcessStartInfo (
                        FileName = "cmd",
                        Arguments = "/c cd C:\perso\like && elm-make.exe main.elm --yes",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true )
                        
              let proc = new System.Diagnostics.Process()
              proc.StartInfo <- pi

              proc.Start() |> ignore

              while not proc.StandardOutput.EndOfStream do
                let line = proc.StandardOutput.ReadLine()
                messages.OnNext <| CommandOutput line
              while not proc.StandardError.EndOfStream do
                let line = proc.StandardError.ReadLine()
                messages.OnNext <| CommandOutput line
              
            )





//type App = XAML<"App.xaml">


[<STAThread>]
[<EntryPoint>]
let main argv =
    
    let addSyntax (f:string) name ext = 
        use reader = new XmlTextReader(f)
        let customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting(name, ext |> List.toArray, customHighlighting);

    addSyntax @"Elm-Mode.xshd" "Elm"  [".elm"]
    addSyntax @"Html-Mode.xshd" "Html"  [".html";"*.htm"]

    let color s = new SimpleHighlightingBrush(downcast ColorConverter.ConvertFromString(s))
    let defaultColor = color "#FF0000"
    let colors = 
        [
        "AccessKeywords", defaultColor;
        "AccessModifiers", defaultColor;
        "AddedText", defaultColor;
        "ASPSection", defaultColor;
        "ASPSectionStartEndTags", defaultColor;
        "Assignment", defaultColor;
        "AttributeName", defaultColor;
        "Attributes", defaultColor;
        "AttributeValue", defaultColor;
        "BlockQuote", defaultColor;
        "BooleanConstants", defaultColor;
        "BrokenEntity", defaultColor;
        "CData", defaultColor;
        "Char", defaultColor;
        "Character", defaultColor;
        "CheckedKeyword", defaultColor;
        "Class", color "#A6E22E";
        "Code", defaultColor;
        "Colon", defaultColor;
        "Command", defaultColor;
        "Comment", color "#75715E";
        "CommentTags", defaultColor;
        "CompoundKeywords", defaultColor;
        "Constants", color "#AE81FF";
        "ContextKeywords", defaultColor;
        "ControlFlow", defaultColor;
        "ControlStatements", defaultColor;
        "CurlyBraces", defaultColor;
        "DataTypes", defaultColor;
        "DateLiteral", defaultColor;
        "Digits", color "#AE81FF";
        "DocComment", defaultColor;
        "DocType", defaultColor;
        "Emphasis", defaultColor;
        "Entities", defaultColor;
        "Entity", color "#F92672";
        "EntityReference", defaultColor;
        "ExceptionHandling", defaultColor;
        "ExceptionHandlingStatements", defaultColor;
        "ExceptionKeywords", defaultColor;
        "FileName", defaultColor;
        "Friend", defaultColor;
        "FunctionCall", defaultColor;
        "FunctionKeywords", defaultColor;
        "GetSetAddRemove", defaultColor;
        "GotoKeywords", defaultColor;
        "Header", defaultColor;
        "Heading", defaultColor;
        "HtmlTag", color "#F92672";
        "Image", defaultColor;
        "IterationStatements", defaultColor;
        "JavaDocTags", defaultColor;
        "JavaScriptGlobalFunctions", defaultColor;
        "JavaScriptIntrinsics", defaultColor;
        "JavaScriptKeyWords", defaultColor;
        "JavaScriptLiterals", defaultColor;
        "JavaScriptTag", color "#F92672";
        "JScriptTag", color "#F92672";
        "JumpKeywords", defaultColor;
        "JumpStatements", defaultColor;
        "Keywords", color "#F92672";
        "KnownDocTags", defaultColor;
        "LineBreak", defaultColor;
        "Link", defaultColor;
        "Literals", defaultColor;
        "LoopKeywords", defaultColor;
        "MethodCall", defaultColor;
        "MethodName", defaultColor;
        "Modifiers", defaultColor;
        "Namespace", defaultColor;
        "NamespaceKeywords", defaultColor;
        "NullOrValueKeywords", defaultColor;
        "NumberLiteral", color "#AE81FF";
        "OperatorKeywords", defaultColor;
        "Operators", defaultColor;
        "OtherTypes", defaultColor;
        "Package", defaultColor;
        "ParameterModifiers", defaultColor;
        "Position", defaultColor;
        "Preprocessor", defaultColor;
        "Property", defaultColor;
        "Punctuation", defaultColor;
        "ReferenceTypeKeywords", defaultColor;
        "ReferenceTypes", defaultColor;
        "Regex", color "#F6AA11";
        "RemovedText", defaultColor;
        "ScriptTag", color "#F92672";
        "SelectionStatements", defaultColor;
        "Selector", defaultColor;
        "Slash", defaultColor;
        "String", color "#66D9EF";
        "StrongEmphasis", defaultColor;
        "Tags", color "#F92672";
        "This", defaultColor;
        "ThisOrBaseReference", defaultColor;
        "TrueFalse", defaultColor;
        "TypeKeywords", defaultColor;
        "UnchangedText", defaultColor;
        "UnknownAttribute", defaultColor;
        "UnknownScriptTag", defaultColor;
        "UnsafeKeywords", defaultColor;
        "Value", defaultColor;
        "ValueTypeKeywords", defaultColor;
        "ValueTypes", defaultColor;
        "Variable", defaultColor;
        "VBScriptTag", defaultColor;
        "Visibility", defaultColor;
        "Void", defaultColor;
        "XmlDeclaration", defaultColor;
        "XmlPunctuation", defaultColor;
        "XmlString", defaultColor;
        "XmlTag", color "#F92672"] 
        |> Map.ofList
    

    for def in HighlightingManager.Instance.HighlightingDefinitions do
        for c in def.NamedHighlightingColors do 
            c.Foreground <- colors.[c.Name]
    
//                c.Foreground <- new SimpleHighlightingBrush(Color.FromRgb (byte 248,byte 248,byte 242))


    let w =  new Window(Title="F# is fun!",Width=260., Height=420.)
    w.WindowState <- WindowState.Maximized
    w.Show()
    w.Content <- List.head <| resolve [] [ui intialState] [] 


    let saveCommand = new KretschIT.WP_Fx.UI.Commands.RelayCommand(fun e -> messages.OnNext(SaveFile)  )
    w.InputBindings.Add(new KeyBinding(saveCommand,Key.S,ModifierKeys.Control)) |> ignore

    let textChanged = messages.Where(function | TextChanged _ -> true | _ -> false).Throttle(TimeSpan.FromSeconds(1.))
    //let commandOutputs = messages.Where(function | CommandOutput s -> true | _ -> false).Buffer(TimeSpan.FromSeconds(2.),DispatcherScheduler.Current).Select(fun b -> CommandOutputBuffered b )
    let optMessages = messages.Where(function | TextChanged _ -> false | _ -> true).Merge(textChanged)

    optMessages
        .Scan(intialState,
            fun state cmd ->
                match cmd with 
                | TextChanged doc ->
                    let starize (t:String) = if t.EndsWith("*") then t else t+"*"
                    {state with openFiles= List.map(fun (t,d) -> if d = doc then (starize t,d) else (t,d)) state.openFiles }      
                | OpenFile s ->
                    let content = IO.File.ReadAllText(s)
                    let doc = new ICSharpCode.AvalonEdit.Document.TextDocument(content)
                    doc.FileName <- s
                    {state with openFiles=state.openFiles@[(s,doc)] } 
                | SaveFile -> 
                    let unstarize (s:String) = s.Replace("*","")
                    let (t,doc) = List.find (fun (t,doc) -> doc = state.current) state.openFiles
                    IO.File.WriteAllText(doc.FileName,doc.Text)

                    for cmd in state.watches do 
                        let cwd s = "cd " +  IO.Path.GetDirectoryName doc.FileName + ";" + s
                        cmd.Replace("%currentpath%", doc.FileName) |> cwd |> run |> ignore

                    {state with openFiles= List.map(fun (t,d) -> if d = doc then (unstarize t,d) else (t,d)) state.openFiles }      
                | CommandOutput s ->
                    {state with consoleOutput = s}
                | DocSelected s ->
                    {state with current = s}
                | other -> state  )
        .Scan((ui intialState,ui intialState), fun (prevdom,newdom) state -> (newdom,ui state) )
        .ObserveOnDispatcher()
        .Subscribe(function (p,c) -> w.Content <- List.head <| resolve [p] [c] [downcast w.Content]  )
        |>ignore

    let app = new Application()
    app.Run(w)
//    App().Root.Run()