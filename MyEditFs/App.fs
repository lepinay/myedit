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
open ICSharpCode.AvalonEdit.Search
open System.Windows.Shapes



//#region helpers
type Column = GridLength
type Row = GridLength
type Title = String
type SplitterDirection = Horizontal | Vertical

type EditorElement =
    { doc: TextDocument;
        selection: (int*int)list } 

[<CustomEquality; CustomComparison>]
type TextAreaElement =
    {   text: string;
        onReturn : (string -> unit) option
        onTextChanged : (string -> unit) option } 
    override x.Equals(yobj) =
        match yobj with
        | :? TextAreaElement as y -> x.text = y.text
        | _ -> false
 
    override x.GetHashCode() = hash x.text
    interface System.IComparable with
        member x.CompareTo yobj =
            match yobj with
            | :? TextAreaElement as y -> compare x.text y.text
            | _ -> invalidArg "yobj" "cannot compare values of different types"

[<CustomEquality; CustomComparison>]
type TabItemElement =
    { 
        id:string
        title: string
        selected:bool
        element: Element
        onSelected : (unit -> unit) option
        onClose : (unit -> unit) option } 
    override x.Equals(yobj) =
        match yobj with
        | :? TabItemElement as y -> x.selected = y.selected && x.title = y.title && x.element = y.element
        | _ -> false
 
    override x.GetHashCode() = (hash x.selected) ^^^ (hash x.title) ^^^ (hash x.element)
    interface System.IComparable with
        member x.CompareTo yobj =
            match yobj with
            | :? TabItemElement as y -> compare x.title y.title
            | _ -> invalidArg "yobj" "cannot compare values of different types"

// http://blogs.msdn.com/b/dsyme/archive/2009/11/08/equality-and-comparison-constraints-in-f-1-9-7.aspx
// TODO separate pure UI command from application business commands
and Command =
    | BrowseFile
    | BrowseFolder
    | SaveFile
    | OpenFile of string
    | TextChanged of TextDocument
    | CommandOutput of string
    | DocSelected of TextDocument
    | DocClosed of TextDocument
    | Search of string
    | SearchNext of string
// TODO see before, elements should have their own commands
and Element = 
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
    | Editor of EditorElement
    | TabItem of TabItemElement
    | TextArea of TextAreaElement
    | Tab of Element list
    | Scroll of Element

type Directory = 
    | None
    | Directory of string*Directory list*string list

type TabState = {
    path:string
    doc:TextDocument
    search:string
    selectedText:(int*int)list
}

type EditorState = {
    openFiles:TabState list
    current:TextDocument
    watches : (string) list
    consoleOutput : string
    currentFolder: Directory
}


let messages = new Reactive.Subjects.Subject<Command>()
let debug s = messages.OnNext <| CommandOutput (sprintf "[%A] %s:" DateTime.Now s)

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
    let filesToTabs docState = 
        TabItem {
            id=docState.doc.FileName
            title=IO.Path.GetFileName(docState.path)
            selected = state.current = docState.doc
            element=Dock
                [
//                    Docked(TextArea {
//                                        text=docState.search
//                                        onTextChanged=Some(fun s -> messages.OnNext(Search s))
//                                        onReturn=Some(fun s -> messages.OnNext(SearchNext s)) },Dock.Top)
                    Editor {doc=docState.doc;selection=docState.selectedText}]
            onSelected = Some( fun () -> messages.OnNext(DocSelected docState.doc) )
            onClose = Some( fun () -> messages.OnNext(DocClosed docState.doc) )}
    let tabs = 
        state.openFiles 
        |> List.map filesToTabs
    let rec makeTree = function
        | None -> []
        | Directory (p, folders, files) -> 
            let filest = List.map( fun f -> TreeItem(f,[])) files
            let folderst = List.map makeTree folders |> List.concat
            [TreeItem(p,  folderst @ filest )]

    let tree = Tree <| makeTree state.currentFolder
    Dock [
        Docked(Menu [MenuItem ("File",[MenuItem ("Open file",[], [BrowseFile], "" );MenuItem ("Open folder",[], [BrowseFolder], "");MenuItem ("Save",[], [SaveFile], "Ctrl+S")], [], "")],Dock.Top)
        Grid ([GridLength(1.,GridUnitType.Star);GridLength(0.5);GridLength(9.,GridUnitType.Star)],[],[
                Column(tree,0)
                Column(Splitter Vertical,1)
                Column(
                    Grid ([GridLength(1.,GridUnitType.Star)],
                            [GridLength(2.,GridUnitType.Star);GridLength(0.5);GridLength(1.,GridUnitType.Star)],
                            [
                                Row(Tab tabs,0)
                                Row(Splitter Horizontal,1)
                                Row(Scroll(TextArea {text = state.consoleOutput;onTextChanged = Option.None;onReturn = Option.None}),2)
                            ]),2)
            ])
    ]

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
    "FindHighlight", color "#FFE792";
    "FindHighlightForeground", color "#000000";
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
            MahApps.Metro.Controls.TabControlHelper.SetIsUnderlined(d,true);
            for x in xs do d.Items.Add (render x) |> ignore
            d :> UIElement
        | TabItem {title=title;element=e;onSelected=com;onClose=close;selected=selected} ->
            let ti = new TabItem()
            ti.Content <- render e
            let closeButton = new MyEdit.Wpf.Controls.TabItem()
            closeButton.TabTitle.Text <- title
            match close with
                | Some(close) -> closeButton.TabClose.MouseDown |> Observable.subscribe(fun e -> close()) |> ignore
                | Option.None -> ()
            ti.Header <- closeButton
            ti.IsSelected <- selected
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
        | Editor {doc=doc;selection=selection} ->
            let editor = new TextEditor();

            editor.SyntaxHighlighting <- HighlightingManager.Instance.GetDefinitionByExtension(IO.Path.GetExtension(doc.FileName));
            editor.Document <- doc;
            editor.FontFamily <- FontFamily("Consolas")
            editor.TextChanged |> Observable.subscribe(fun e -> messages.OnNext(TextChanged doc)  ) |> ignore
            editor.ShowLineNumbers <- true
            editor.Background <- bgColor
            editor.Foreground <- fgColor
            editor.Options.ConvertTabsToSpaces <- true
            editor.Options.EnableHyperlinks <- false
//            editor.Options.ShowColumnRuler <- true
            let sp = SearchPanel.Install(editor)
            let brush = colors.["FindHighlight"]
            sp.MarkerBrush  <- brush.GetBrush(null)


            let foldingManager = FoldingManager.Install(editor.TextArea);
            let foldingStrategy = new XmlFoldingStrategy();
            foldingStrategy.UpdateFoldings(foldingManager, editor.Document);

            editor.Options.EnableRectangularSelection <- true

            editor :> UIElement
        | TextArea {text=s;onTextChanged=textChanged;onReturn=returnKey} -> 
            let tb = new TextBox(Background = bgColor, Foreground = fgColor)
            tb.Text <- s
            tb.FontFamily <- FontFamily("Consolas")
            match textChanged with
                | Some(action) ->  tb.TextChanged |> Observable.subscribe(fun e -> action tb.Text)  |> ignore
                | Option.None -> ()
            match returnKey with
                | Some(action) ->  tb.KeyDown |> Observable.subscribe(fun e -> action tb.Text)  |> ignore
                | Option.None -> ()
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

// Goal of this method is to avoid to call render as much as possible and instead reuse as much as already existing WPF controls between 
// virtual dom changes
// Calling render is expensive as it will create new control and trigger reflows
let rec resolve (prev:Element list) (curr:Element list) (screen:UIElement list) : UIElement list = 
    if prev = curr then screen 
    else 
        match (prev,curr,screen) with
            | (x::xs,y::ys,z::zs) when x = y -> z::resolve xs ys zs
            | ((TabItem {title=ta;element=ea;id=ida})::xs,(TabItem {title=tb;element=eb;selected=selb;id=idb})::ys,z::zs) when ida = idb -> 
                let ti = z :?> TabItem
                let header = ti.Header :?> MyEdit.Wpf.Controls.TabItem
                header.TabTitle.Text <- tb
                ti.IsSelected <- selb
                ti.Content <- List.head <| resolve [ea] [eb] [ti.Content :?> UIElement]

                // This won't handle the case where tabs were reordered since ys wont' have chance to go again trought all xs !
                z::resolve xs ys zs
            | ((TabItem {id=ida})::xs,(TabItem {id=idb} as y)::ys,z::zs) when ida <> idb -> resolve xs (y::ys) zs
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
            | ((Editor {doc=tda;selection=sela})::xs,(Editor {doc=tdb;selection=selb})::ys,z::zs)  -> 
                let editor = z :?> TextEditor
                match selb with
                    | [(s,e)] -> editor.Select(s,e)
                    | [] -> editor.SelectionLength <- 0
                    | multiselect -> ()
                z::resolve xs ys zs
            | ((Scroll a)::xs,(Scroll b)::ys,z::zs) -> 
                let scroll = z :?> ScrollViewer
                scroll.Content <- List.head <| resolve [a] [b] [scroll.Content :?> UIElement]
                scroll.ScrollToBottom()
                (scroll:>UIElement)::resolve xs ys zs
            | ((TextArea {text=a})::xs,(TextArea {text=b})::ys,z::zs)  -> 
                let tb = z :?> TextBox
                tb.AppendText("\n"+b)
                z::resolve xs ys zs
            | ([],y::ys,[]) -> 
                debug <| sprintf "render %A" y 
                (render y)::resolve [] ys []
            | ([],[],[]) -> []

            // UI element removed
            | (x::xs,[],zs) -> 
                // for z in zs do ??? what should we do when ui element are removed from the UI, right now they should be GCed
                // will need to check w dont have memory leaks, speialy with all the event subscribers that could be still attached
                // scary place here
                []
            
            | (_,y::ys,_) -> 
                failwith <| sprintf "unable to reuse from %A" y
                (render y)::resolve [] ys []
            | other -> failwith <| sprintf "not handled:\nPREV\n%A\nCURR\n%A\nSCREEN\n%A" prev curr screen

// elm-make main.elm --yes
let intialState = {
    openFiles=[]
    watches=[("elm-make %currentpath% --yes")]
    consoleOutput=""
    current=null
    currentFolder=Directory ("Code", [Directory ("Src", [], ["file1.test";"file2.test"]) ], ["file1";"file2"]) 
    }


//let myHost = new MyHost(fun s -> 
//    Application.Current.Dispatcher.InvokeAsync(
//        fun () ->
//            debug s
//            messages.OnNext <| CommandOutput s) |> ignore
//    )
//let myRunSpace = RunspaceFactory.CreateRunspace(myHost);
//myRunSpace.Open();

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
let rec oneBefore files doc = 
    match files with
        | [] -> null
        | [current] when current.doc = doc -> null
        | before::current::xs when current.doc = doc -> before.doc
        | current::next::xs when current.doc = doc -> next.doc
        | x::xs -> oneBefore xs doc
        | [x] -> null

let renderApp (w:Window) =
    
    let addSyntax (f:string) name ext = 
        use reader = new XmlTextReader(f)
        let customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting(name, ext |> List.toArray, customHighlighting);

    addSyntax @"Elm-Mode.xshd" "Elm"  [".elm"]
    addSyntax @"Html-Mode.xshd" "Html"  [".html";"*.htm"]

    for def in HighlightingManager.Instance.HighlightingDefinitions do
        for c in def.NamedHighlightingColors do 
            c.Foreground <- colors.[c.Name]
    
//                c.Foreground <- new SimpleHighlightingBrush(Color.FromRgb (byte 248,byte 248,byte 242))


    w.Content <- List.head <| resolve [] [ui intialState] [] 


    let saveCommand = new KretschIT.WP_Fx.UI.Commands.RelayCommand(fun e -> messages.OnNext(SaveFile)  )
//    let searchCommand = new KretschIT.WP_Fx.UI.Commands.RelayCommand(fun e -> messages.OnNext(Search)  )
    w.InputBindings.Add(new KeyBinding(saveCommand,Key.S,ModifierKeys.Control)) |> ignore
//    w.InputBindings.Add(new KeyBinding(searchCommand,Key.F,ModifierKeys.Control)) |> ignore

    let textChanged = messages.Where(function | TextChanged _ -> true | _ -> false).Throttle(TimeSpan.FromSeconds(1.))
    //let commandOutputs = messages.Where(function | CommandOutput s -> true | _ -> false).Buffer(TimeSpan.FromSeconds(2.),DispatcherScheduler.Current).Select(fun b -> CommandOutputBuffered b )
    let optMessages = messages.Where(function | TextChanged _ -> false | _ -> true).Merge(textChanged)

    optMessages
        .Scan(intialState,
            fun state cmd ->
                match cmd with 
                | TextChanged doc ->
                    let starize (t:String) = if t.EndsWith("*") then t else t+"*"
                    {state with openFiles= List.map(fun tstate -> if tstate.doc = doc then {tstate with path = starize tstate.path} else tstate) state.openFiles }      
                | OpenFile s ->
                    let content = IO.File.ReadAllText(s)
                    let doc = new ICSharpCode.AvalonEdit.Document.TextDocument(content)
                    doc.FileName <- s
                    {state with current=doc; openFiles=state.openFiles@[{path=s;doc=doc;search="";selectedText=[]}] } 
                | SaveFile -> 
                    let unstarize (s:String) = s.Replace("*","")
                    let tstate = List.find (fun tsate -> tsate.doc = state.current) state.openFiles
                    IO.File.WriteAllText(tstate.doc.FileName,tstate.doc.Text)

                    for cmd in state.watches do 
                        let cwd s = "cd " +  IO.Path.GetDirectoryName tstate.doc.FileName + ";" + s
                        cmd.Replace("%currentpath%", tstate.doc.FileName) |> cwd |> run |> ignore

                    {state with openFiles= List.map(fun tstate' -> if tstate.doc = tstate'.doc then {tstate' with path = unstarize tstate'.path} else tstate') state.openFiles }      
                | CommandOutput s ->
                    {state with consoleOutput = s}
                | DocSelected s ->
                    {state with current = s}
                | DocClosed s ->
                    {state with openFiles = state.openFiles |> List.filter(fun tstate -> tstate.doc <> s ); current = oneBefore state.openFiles s   }
                | Search s ->
                    let res = state.current.Text.IndexOf(s)
                    if res >= 0 then
                        let tstate = List.find (fun tsate -> tsate.doc = state.current) state.openFiles
                        {state with openFiles= List.map (fun tab -> if tab.doc = state.current then {tab with selectedText = [(res,s.Length)] } else tab ) state.openFiles }
                    else state
                | SearchNext s ->
                    let tstate = List.find (fun tsate -> tsate.doc = state.current) state.openFiles
                    match tstate.selectedText with
                        | [(idx,len)] ->
                            let res = state.current.Text.IndexOf(s,idx+len)
                            if res >= 0 then
                                let tstate = List.find (fun tsate -> tsate.doc = state.current) state.openFiles
                                {state with openFiles= List.map (fun tab -> if tab.doc = state.current then {tab with selectedText = [(res,s.Length)] } else tab ) state.openFiles }
                            else state
                        | other -> state
                | other -> 
                    debug <| sprintf "not handled %A" other
                    state  )
        .Scan((ui intialState,ui intialState), fun (prevdom,newdom) state -> (newdom,ui state) )
        .ObserveOnDispatcher()
        .Subscribe(function (p,c) -> w.Content <- List.head <| resolve [p] [c] [downcast w.Content]  )
        |>ignore

