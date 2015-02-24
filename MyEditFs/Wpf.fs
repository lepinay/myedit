module Wpf
open Dom
open System.Windows
open System.Windows.Media
open ICSharpCode.AvalonEdit.Highlighting
open System.Windows.Controls
open ICSharpCode.AvalonEdit
open ICSharpCode.AvalonEdit.Search
open ICSharpCode.AvalonEdit.Folding
open System
open System.Windows.Documents

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
    "Error", color "#FF0000";
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



let collToList (coll:UIElementCollection) : UIElement list =
    seq { for c in coll -> c } |> Seq.toList

let itemsToList (coll:ItemCollection) =
    seq { for c in coll -> c :?> UIElement } |> Seq.toList


type VirtualDomNodeElement = {
    element : Element
    ui : UIElement
    subs : IDisposable list
    childrens:VirtualDom list
}

and VirtualDom = 
    | Node of VirtualDomNodeElement

let appendChildrens add childrens =
    for child in childrens do 
        match child with
            | Node{ui=ui} ->  add ui |> ignore

let uielt dom = 
    match dom with
        | Node {ui=elt} -> elt


let rec render ui : VirtualDom = 

    let bgColor = new SolidColorBrush(Color.FromRgb (byte 39,byte 40,byte 34))
    let fgColor = new SolidColorBrush(Color.FromRgb (byte 248,byte 248,byte 242))

    match ui with
        | Dock xs -> 
            let d = new DockPanel(LastChildFill=true)
            let childrens = xs |> List.map render
            appendChildrens d.Children.Add childrens
            Node {element=ui; ui=d :> UIElement;subs=[];childrens=childrens}
//        | Terminal -> new Terminal() :> UIElement
        | Tab xs -> 
            let d = new TabControl()   
            MahApps.Metro.Controls.TabControlHelper.SetIsUnderlined(d,true);
            let childrens = xs |> List.map render
            appendChildrens d.Items.Add childrens
            Node {element=ui; ui=d :> UIElement;subs=[];childrens=childrens}
        | TabItem {title=title;element=e;onSelected=com;onClose=close;selected=selected} ->
            let ti = new TabItem()
            let child = render e
            ti.Content <- uielt child
            let closeButton = new MyEdit.Wpf.Controls.TabItem()
            closeButton.TabTitle.Text <- title
            let subs = 
                match close with
                    | Some(close) -> [closeButton.TabClose.MouseDown |> Observable.subscribe(fun e -> close())]
                    | Option.None -> []
                @
                match com with
                    | Some(selected) -> [closeButton.MouseDown |> Observable.subscribe(fun e -> selected())]
                    | Option.None -> []
            ti.Header <- closeButton
            ti.IsSelected <- selected
            Node {element=ui; ui=ti :> UIElement;subs=subs;childrens=[child]}
        | Menu xs ->
            let m = new Menu()
            let childrens = xs |> List.map render
            appendChildrens m.Items.Add childrens
            Node {element=ui; ui=m :> UIElement;subs=[];childrens=childrens}
        | MenuItem {title=title;elements=xs;onClick=actions;gesture=gestureText} -> 
            let mi = Controls.MenuItem(Header=title) 
            mi.InputGestureText <- gestureText
            let childrens = xs |> List.map render
            appendChildrens mi.Items.Add childrens
            let subs = 
                match actions with 
                    | Some action -> [mi.Click |> Observable.subscribe(fun e -> action())]
                    | Option.None -> []
            Node {element=ui; ui=mi :> UIElement;subs=subs;childrens=[]}
        | Grid (cols,rows,xs) ->
            let g = new Grid()
            let sizeToLength = function
                | Star n -> GridLength(n,GridUnitType.Star)
                | Pixels n -> GridLength(n)
            for row in rows do g.RowDefinitions.Add(new RowDefinition(Height=sizeToLength row))
            for col in cols do g.ColumnDefinitions.Add(new ColumnDefinition(Width=sizeToLength col))
            let childrens = xs |> List.map render
            appendChildrens g.Children.Add childrens
            Node {element=ui; ui=g :> UIElement;subs=[];childrens=childrens}
        | Docked (e,d) ->
            let dom = render e
            let elt = uielt dom
            DockPanel.SetDock(elt,d)
            Node {element=ui;ui=elt;subs=[];childrens=[dom]}
        | Column (e,d) ->
            let dom = render e
            let elt = uielt dom
            Grid.SetColumn(elt,d)
            Node {element=ui;ui=elt;subs=[];childrens=[dom]}
        | Row (e,d) ->
            let dom = render e
            let elt = uielt dom
            Grid.SetRow(elt,d)
            Node {element=ui;ui=elt;subs=[];childrens=[dom]}
        | Splitter Vertical ->
            let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Center,ResizeDirection=GridResizeDirection.Columns,ShowsPreview=true,Width=5.)
            gs.Background <- (color "#252525").GetBrush(null)
            Node {element=ui; ui=gs :> UIElement;subs=[];childrens=[]}
        | Splitter Horizontal ->
            let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Stretch,ResizeDirection=GridResizeDirection.Rows,ShowsPreview=true,Height=5.)
            gs.Background <- (color "#252525").GetBrush(null)
            Node {element=ui; ui=gs :> UIElement;subs=[];childrens=[]}
        | Tree xs ->
            let t = new TreeView()
            let childrens = xs |> List.map render
            appendChildrens t.Items.Add childrens
            Node {element=ui; ui=t :> UIElement;subs=[];childrens=childrens}
        | TreeItem {title=title;elements=xs;onTreeItemSelected=onTreeItemSelected} ->
            let ti = new TreeViewItem()
            ti.Header <- title
            let subs = 
                match onTreeItemSelected with
                    | Some(action) -> [ti.Selected |> Observable.subscribe(fun e ->e.Handled <- true; action())]
                    | None -> []
            let childrens = xs |> List.map render
            appendChildrens ti.Items.Add childrens
            Node {element=ui; ui=ti :> UIElement;subs=subs;childrens=childrens}

        | Editor {doc=doc;selection=selection;textChanged=textChanged} ->
            let editor = new TextEditor();

            editor.SyntaxHighlighting <- HighlightingManager.Instance.GetDefinitionByExtension(IO.Path.GetExtension(doc.FileName));
            editor.Document <- doc;
            editor.FontFamily <- FontFamily("Consolas")
            let subs = [editor.TextChanged |> Observable.subscribe(fun e -> textChanged(doc)  )]
            editor.ShowLineNumbers <- true
            editor.Background <- bgColor
            editor.Options.ShowTabs <- true
            editor.Options.IndentationSize <- 2
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
            Node {element=ui; ui=editor :> UIElement;subs=subs;childrens=[]}
        | AppendConsole {text=s;onTextChanged=textChanged;onReturn=returnKey} -> 
            let editor = new TextEditor();

            editor.IsReadOnly <- true
            editor.FontFamily <- FontFamily("Consolas")
            editor.Background <- bgColor
            editor.Foreground <- fgColor
            editor.SyntaxHighlighting <- HighlightingManager.Instance.GetDefinitionByExtension(".console");
            editor.Options.EnableRectangularSelection <- true

            editor.AppendText s
            
            let subs = 
                match textChanged with
                    | Some(action) -> [editor.TextChanged |> Observable.subscribe(fun e -> action(editor.Text)  )]
                    | Option.None -> []
            Node {element=ui; ui=editor :> UIElement;subs=subs;childrens=[]}
        | Scroll e ->
            let scroll = new ScrollViewer()
            let child = render e
            scroll.Content <- uielt child
            Node {element=ui; ui=scroll :> UIElement;subs=[];childrens=[child]}
        | other -> failwith "not handled"


// Goal of this method is to avoid to call render as much as possible and instead reuse as much as already existing WPF controls between 
// virtual dom changes
// Calling render is expensive as it will create new control and trigger reflows
let domListToElemList doms =
    doms
    |> List.map (fun (Node{element=d}) -> d )

let clearSubs (subs:IDisposable seq) = 
    for sub in subs do sub.Dispose()

let rec resolve (prev:VirtualDom list) (curr:Element list) : VirtualDom list = 
    if domListToElemList prev = curr then prev 
    else 
        match (prev,curr) with
            | ([],[]) -> []
            | ([],y::ys) -> 
//                Console.WriteLine (sprintf "render %A" y )
                (render y)::resolve [] ys
            // UI element removed
            | (x::xs,[]) -> 
                // for z in zs do ??? what should we do when ui element are removed from the UI, right now they should be GCed
                // will need to check w dont have memory leaks, speialy with all the event subscribers that could be still attached
                // scary place here
                []
            | (Node {element=x} as z::xs,y::ys) when x = y -> 
                z::resolve xs ys
            | (Node {element=TabItem {title=ta;id=ida};ui=ui;childrens=ea} as z::xs,(TabItem {title=tb;element=eb;selected=selb;id=idb} as tib)::ys) when ida = idb -> 
                let ti = ui :?> TabItem
                let header = ti.Header :?> MyEdit.Wpf.Controls.TabItem
                header.TabTitle.Text <- tb
                ti.IsSelected <- selb
                let childrens = resolve ea [eb]
                ti.Content <- uielt (List.head <| childrens)

                // This won't handle the case where tabs were reordered since ys wont' have chance to go again trought all xs !
                Node{element=tib;ui=ui;subs=[];childrens=childrens}::resolve xs ys
            | (Node {element=TabItem {id=ida}}::xs,(TabItem {id=idb} as y)::ys) when ida <> idb -> resolve xs (y::ys)
            | (Node {element=Tab _;ui=z;childrens=a}::xs,(Tab b)::ys) -> 
                let tab = z :?> TabControl     
                let childrens = resolve a b
                let childrensui = childrens |> List.map (fun (Node{ui=c}) -> c )
                
                for c in childrensui do 
                    if(not (tab.Items.Contains(c))) then tab.Items.Add(c) |> ignore
                
                let removals =  List.fold (fun state elt -> if childrensui |> List.exists (fun c -> c = elt) then state else elt::state ) [] (itemsToList tab.Items)
                for r in removals do tab.Items.Remove(r)    
                                
                Node{element=Tab b;ui=z;childrens=childrens;subs=[]}::resolve xs ys
            | (Node{element=Dock _;ui=z;childrens=a}::xs,(Dock b as dockb)::ys) -> 
                let dock = z :?> DockPanel     
                let childrens = resolve a b 
                for Node{ui=c} in childrens do 
                    if(not (dock.Children.Contains(c))) then dock.Children.Add(c) |> ignore
                Node{element=dockb;ui=z;childrens=childrens;subs=[]}::resolve xs ys
            | (Node{element=Docked (da,pa);childrens=a;ui=z}::xs,(Docked (db,pb))::ys) when pa = pb -> Node{element=Docked(db,pb);ui=z;subs=[];childrens=resolve a [db]} :: resolve xs ys
            | (Node{element=Column (_,pa);childrens=a;ui=z}::xs,(Column (b,pb))::ys) when pa = pb -> Node{element=Column(b,pb);ui=z;subs=[];childrens=resolve a [b]} :: resolve xs ys
            | (Node{element=Row (_,pa);childrens=a;ui=z}::xs,(Row (b,pb))::ys) when pa = pb -> Node{element=Row(b,pb);ui=z;subs=[];childrens=resolve a [b]} :: resolve xs ys
            | (Node{element=Grid (acols,arows,_);ui=z;childrens=a}::xs,(Grid (bcols,brows,b) as gridb)::ys) when acols = bcols && arows = brows -> 
                let grid = z :?> Grid     
                let childrens = (collToList grid.Children)
                let childrens = resolve a b 
                for Node{ui=c} in childrens do 
                    if(not (grid.Children.Contains(c))) then grid.Children.Add(c) |> ignore
                Node{element=gridb;ui=z;childrens=childrens;subs=[]}::resolve xs ys
            | (Node{element=Editor {doc=tda;selection=sela};ui=z}::xs,(Editor {doc=tdb;selection=selb} as editorb)::ys)  -> 
                let editor = z :?> TextEditor
                match selb with
                    | [(s,e)] -> editor.Select(s,e)
                    | [] -> editor.SelectionLength <- 0
                    | multiselect -> ()
                Node{element=editorb;ui=z;childrens=[];subs=[]}::resolve xs ys
            | (Node {element=Scroll _;ui=z;childrens=a}::xs,(Scroll b)::ys) -> 
                let scroll = z :?> ScrollViewer
                let child = resolve a [b]
                scroll.Content <- uielt <| List.head child
                scroll.ScrollToBottom()
                Node{element=Scroll b;ui=z;childrens=child;subs=[]}::resolve xs ys
            | (Node{element=AppendConsole {text=a};ui=z}::xs,(AppendConsole {text=b} as textb)::ys)  -> 
                let tb = z :?> TextEditor
                tb.AppendText("\n"+b)
                tb.ScrollToEnd()
                Node{element=textb;ui=z;childrens=[];subs=[]}::resolve xs ys
            | (Node{element=Tree _;ui=z;childrens=a}::xs,(Tree b as treeb)::ys) ->
                let tree = z :?> TreeView     
                let childrens = (itemsToList tree.Items)
                tree.Items.Clear()
                let childrens = resolve a b
                for c in  childrens do tree.Items.Add(uielt c) |> ignore
                Node{element=treeb;ui=z;childrens=childrens;subs=[]}::resolve xs ys
            | (Node{element=TreeItem {elements=_};ui=z;childrens=a;subs=subs}::xs,(TreeItem {title=tb;elements=b;onTreeItemSelected=action} as tib)::ys) ->
                clearSubs subs
                let tree = z :?> TreeViewItem   
                tree.Header <- tb
                let newsubs = 
                    match action with 
                        | Some(act) -> [tree.Selected |> Observable.subscribe(fun e ->e.Handled <- true; act() )] 
                        | None -> []
                let childrens = (itemsToList tree.Items)
                tree.Items.Clear()
                let childrens = resolve a b
                for c in  childrens do tree.Items.Add(uielt c) |> ignore
                Node{element=tib;ui=z;subs=newsubs;childrens=childrens}::resolve xs ys
            
            | (_,y::ys) -> 
                failwith <| sprintf "unable to reuse from %A" y
                (render y)::resolve [] ys
            | other -> failwith <| sprintf "not handled:\nPREV\n%A\nCURR\n%A" prev curr

