module main 

open ICSharpCode.AvalonEdit.Document
open System

open Dom
open Wpf
open System.Windows
open System.Xml
open ICSharpCode.AvalonEdit.Highlighting
open System.Windows.Input
open System
open System.Linq
open System.Windows.Controls  
open System.Reactive.Linq
open System.Threading.Tasks
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open System.Management

type Directory = 
    | None
    | Directory of string*Directory list*string list

type Command =
    | SaveFile
    | OpenFile of string
    | OpenFolder of string
    | TextChanged of TextDocument
    | CommandOutput of string
    | DocSelected of TextDocument
    | DocClosed of TextDocument
    | Search of string
    | SearchNext of string
    | SelectFile of string
    | ExpandFolder of Directory
    | ShellCommandUpdating of String
    | ShellCommandConfirmed of string
    | ShellStartDaemon of string
    | Close


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
    prompt : String
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
    let dialog = new System.Windows.Forms.FolderBrowserDialog();
    match dialog.ShowDialog() with
        | Forms.DialogResult.OK -> messages.OnNext(OpenFolder dialog.SelectedPath)
        | other -> ()

let saveFile () = 
    debug "save a file"
    ()

// We could optimize this by diffing the state to the previous state and generate only the updated dom
// like we already do for the Dom to WPF transformation
let ui (state:EditorState) = 
    let filesToTabs docState = 
        Dom.TabItem {
            id=docState.doc.FileName
            title=IO.Path.GetFileName(docState.path)
            selected = state.current = docState.doc
            element=Dom.Dock
                [
//                    Docked(TextArea {
//                                        text=docState.search
//                                        onTextChanged=Some(fun s -> messages.OnNext(Search s))
//                                        onReturn=Some(fun s -> messages.OnNext(SearchNext s)) },Dock.Top)
                    Editor {doc=docState.doc;selection=docState.selectedText;textChanged=fun doc -> messages.OnNext(TextChanged doc)}]
            onSelected = Some( fun () -> messages.OnNext(DocSelected docState.doc) )
            onClose = Some( fun () -> messages.OnNext(DocClosed docState.doc) )}
    let tabs = 
        state.openFiles 
        |> List.map filesToTabs
    let rec makeTree = function
        | None -> []
        | Directory (p, folders, files) as folder -> 
            let filest = List.map( fun f -> TreeItem{title=IO.Path.GetFileName f;elements=[];onTreeItemSelected=Some(fun () -> messages.OnNext(SelectFile f))}) files
            let folderst = List.map makeTree folders |> List.concat
            [TreeItem{title="\uD83D\uDCC1 " + DirectoryInfo(p).Name;elements=folderst @ filest;onTreeItemSelected = Some(fun () ->messages.OnNext( ExpandFolder folder))}]

    let tree = Tree <| makeTree state.currentFolder
    Dom.Dock [
        Docked(Dom.Menu [Dom.MenuItem {title="File";gesture= "";onClick= Option.None;
                elements=[
                            Dom.MenuItem {title="Open file";elements=[];onClick=Some(openFile);gesture="" };
                            Dom.MenuItem {title="Open folder";elements=[];onClick=Some(openFolder);gesture=""};
                            Dom.MenuItem {title="Save";elements=[];onClick=Some(fun () -> messages.OnNext(SaveFile)); gesture="Ctrl+S"}]}],Dock.Top)
        Dom.Grid ([Star 2.;Pixels 1.;Star 8.],[],
            [
                Dom.Column(tree,0)
                Dom.Column(Splitter Vertical,1)
                Dom.Column(
                    Dom.Grid ([Star 1.],
                            [Star 8.;Pixels 3.;Star 2.;Auto],
                            [
                                Row(Tab tabs,0)
                                Row(Splitter Horizontal,1)
                                Row(AppendConsole {text = state.consoleOutput;onTextChanged = Option.None;onReturn = Option.None},2)
                                Row(Dom.TextBox {text = state.prompt ;onTextChanged = Some(fun s -> messages.OnNext(ShellCommandUpdating s));onReturn = Some(fun s -> messages.OnNext(ShellCommandConfirmed s))},3)
                            ]),2)
            ])
    ]



// elm-make main.elm --yes
let intialState = {
    openFiles=[]
    watches=[("elm-make %currentpath% --yes")]
    consoleOutput=""
    current=null
    currentFolder=None 
    prompt = ""
    }


//let myHost = new MyHost(fun s -> 
//    Application.Current.Dispatcher.InvokeAsync(
//        fun () ->
//            debug s
//            messages.OnNext <| CommandOutput s) |> ignore
//    )
//let myRunSpace = RunspaceFactory.CreateRunspace(myHost);
//myRunSpace.Open();

let rec killProcessAndChildren (pid:int) =
    let searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + (pid.ToString()))
    let moc = searcher.Get();
    for mo in moc do
        killProcessAndChildren(Convert.ToInt32(mo.["ProcessID"]));
       
    let proc = Process.GetProcessById(pid);
    proc.Kill()

let run = 
    Task.Run 
        (fun () ->
                let runProcess (s:string option) = 
                    let pi = 
                        ProcessStartInfo 
                            (
                            FileName = "cmd",
                            Arguments = "/K chcp 65001",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardInput = true,
                            RedirectStandardError = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8,
                            CreateNoWindow = true )
                        
                    let proc = new System.Diagnostics.Process()
                    proc.StartInfo <- pi
                    proc.Start() |> ignore
                    proc.BeginErrorReadLine()
                    proc.BeginOutputReadLine()
  
                    let s1 = proc.OutputDataReceived |> Observable.subscribe(fun e -> messages.OnNext <| CommandOutput e.Data |> ignore)
                    let s2 = proc.ErrorDataReceived |> Observable.subscribe(fun e -> messages.OnNext <| CommandOutput e.Data |> ignore)

                    match s with 
                        | Some(s) -> proc.StandardInput.WriteLineAsync(s) |> ignore
                        | _ -> ()
                    
                    (proc,s1,s2)    
                    
                messages
                    .Scan(runProcess Option.None, (fun (p,s1,s2) msg ->
                        match msg with
                            | ShellStartDaemon s -> 
                                killProcessAndChildren p.Id
                                p.Close()
                                s1.Dispose()
                                s2.Dispose()
                                runProcess (Some (s))
                            | Close ->
                                killProcessAndChildren p.Id
                                p.Close()
                                s1.Dispose()
                                s2.Dispose()
                                (p,s1,s2)
                            | ShellCommandConfirmed s -> 
                                p.StandardInput.WriteLineAsync(s) |> ignore
                                (p,s1,s2)
                            | msg -> (p,s1,s2)
                        ) )                
                    .Subscribe(fun msg -> () ) |> ignore

                )



            

let rec oneBefore files doc = 
    match files with
        | [] -> null
        | [current] when current.doc = doc -> null
        | before::current::xs when current.doc = doc -> before.doc
        | current::next::xs when current.doc = doc -> next.doc
        | x::xs -> oneBefore xs doc
        | [x] -> null


let expandPath s =
    let dirs = 
        System.IO.Directory.EnumerateDirectories(s)
        |> Seq.map(fun p -> Directory(p,[],[])  )
        |> Seq.toList
    Directory(s,dirs,System.IO.Directory.EnumerateFiles(s) |> Seq.toList) 

let rec expandFolder (owner:Directory) (target:Directory) = 

    match (owner,target) with
        | (Directory (apath,adirs,afiles), Directory (bpath,bdirs,bfiles) ) when apath = bpath  -> 
            expandPath apath
        | (Directory (apath,adirs,afiles), Directory (bpath,bdirs,bfiles) ) when bpath.StartsWith(apath)  -> 
            Directory(apath,adirs |> List.map (fun sdir -> expandFolder sdir target),afiles)
        | other  -> owner

let renderApp (w:Window) =
    
    w.Closed |> Observable.subscribe(fun e -> messages.OnNext(Close) ) |> ignore

    let addSyntax (f:string) name ext = 
        use reader = new XmlTextReader(f)
        let customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting(name, ext |> List.toArray, customHighlighting);

    addSyntax @"Elm-Mode.xshd" "Elm"  [".elm"]
    addSyntax @"Haskell-Mode.xshd" "Haskell"  [".hs"]
    addSyntax @"Html-Mode.xshd" "Html"  [".html";"*.htm"]
    addSyntax @"Console-Mode.xshd" "Html"  [".console"]

    for def in HighlightingManager.Instance.HighlightingDefinitions do
        for c in def.NamedHighlightingColors do 
            c.Foreground <- colors.[c.Name]

    let initDom = resolve 0 [] [ui intialState]
    w.Content <- uielt (List.head <| initDom)


    let saveCommand = new KretschIT.WP_Fx.UI.Commands.RelayCommand(fun e -> messages.OnNext(SaveFile)  )
//    let searchCommand = new KretschIT.WP_Fx.UI.Commands.RelayCommand(fun e -> messages.OnNext(Search)  )
    w.InputBindings.Add(new KeyBinding(saveCommand,Key.S,ModifierKeys.Control)) |> ignore
//    w.InputBindings.Add(new KeyBinding(searchCommand,Key.F,ModifierKeys.Control)) |> ignore

    let textChanged = 
        messages
            .Where(function | TextChanged _ -> true | SaveFile -> true | _ -> false)
            .Throttle(TimeSpan.FromSeconds(1.))
            .Where(function | TextChanged _ -> true | _ -> false)
    //let commandOutputs = messages.Where(function | CommandOutput s -> true | _ -> false).Buffer(TimeSpan.FromSeconds(2.),DispatcherScheduler.Current).Select(fun b -> CommandOutputBuffered b )
    let optMessages = messages.Where(function | TextChanged _ -> false | _ -> true).Merge(textChanged)

    optMessages
        .Scan(intialState,
            fun state cmd ->
                match cmd with 
                | TextChanged doc ->
                    let starize (t:String) = if t.EndsWith(" \u2607") then t else t+" \u2607"
                    {state with openFiles= List.map(fun tstate -> if tstate.doc = doc then {tstate with path = starize tstate.path} else tstate) state.openFiles }      
                | OpenFile s | SelectFile s ->
                    let content = IO.File.ReadAllText(s)
                    let doc = new ICSharpCode.AvalonEdit.Document.TextDocument(content)
                    doc.FileName <- s
                    {state with current=doc; openFiles=state.openFiles@[{path=s;doc=doc;search="";selectedText=[]}] } 
                | SaveFile -> 
                    let unstarize (s:String) = s.Replace(" \u2607","")
                    let tstate = List.find (fun tsate -> tsate.doc = state.current) state.openFiles
                    IO.File.WriteAllText(tstate.doc.FileName,tstate.doc.Text)

                    //for cmd in state.watches do 
                    //    let cwd s = "cd " +  IO.Path.GetDirectoryName tstate.doc.FileName + ";" + s
                    //    cmd.Replace("%currentpath%", tstate.doc.FileName) |> cwd |> run |> ignore
                    messages.OnNext(ShellStartDaemon "cd C:\perso\like && runhaskell server.hs") |> ignore
                    //messages.OnNext(ShellCommandConfirmed "cd C:\perso\like && elm-make.exe main.elm --yes") |> ignore

                    {state with openFiles= List.map(fun tstate' -> if tstate.doc = tstate'.doc then {tstate' with path = unstarize tstate'.path} else tstate') state.openFiles }      
                | CommandOutput s ->
                    Console.WriteLine("received {0}", s)
                    {state with consoleOutput = s }
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
                | OpenFolder s ->
                    {state with currentFolder=expandPath s }
                | ShellCommandUpdating s -> {state with prompt = s}
                | ShellCommandConfirmed s -> {state with prompt = ""}
                | ShellStartDaemon s -> {state with prompt = ""}
                | ExpandFolder d ->
                    {state with currentFolder = expandFolder state.currentFolder d })
        .ObserveOnDispatcher()
        .Scan(initDom, fun dom state -> resolve 0 dom [ui state] )
        .Subscribe(function dom -> w.Content <- uielt (List.head dom) )
        |>ignore

