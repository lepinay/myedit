module Dom

open System.Windows.Controls
open ICSharpCode.AvalonEdit.Document
open System.Windows
open System

type Title = String
type SplitterDirection = Horizontal | Vertical
type Size = Star of float | Pixels of float

type Element = 
    | Docked of Element*Dock
    | Column of Element*int
    | Row of Element*int
    | Dock of Element list
    | Menu of Element list
    | MenuItem of MenuItemElement
    | Grid of Size list*Size list*Element list
    | Splitter of SplitterDirection
    | Terminal
    | Tree of Element list
    | TreeItem of TreeItemElement
    | Editor of EditorElement
    | TabItem of TabItemElement
    | TextArea of TextAreaElement
    | Tab of Element list
    | Scroll of Element
and [<CustomEquality;CustomComparison>]EditorElement =
    { doc: TextDocument;
      textChanged:(TextDocument->unit)
      selection: (int*int)list } 
    override x.Equals(yobj) =
        match yobj with
        | :? EditorElement as y -> x.doc = y.doc && x.selection = y.selection
        | _ -> false
 
    override x.GetHashCode() = [hash x.doc;hash x.selection] |> List.reduce (^^^)
    interface System.IComparable with
        member x.CompareTo yobj =
            match yobj with
            | :? EditorElement as y -> compare x.selection y.selection
            | _ -> invalidArg "yobj" "cannot compare values of different types"



and [<CustomEquality; CustomComparison>]MenuItemElement =
    {   title: string
        gesture: string
        elements:Element list
        onClick: (unit -> unit) option } 
    override x.Equals(yobj) =
        match yobj with
        | :? MenuItemElement as y -> x.title = y.title && x.gesture = y.gesture && x.elements = y.elements
        | _ -> false
 
    override x.GetHashCode() = (hash x.title) ^^^ (hash x.gesture) ^^^ (hash x.elements)
    interface System.IComparable with
        member x.CompareTo yobj =
            match yobj with
            | :? MenuItemElement as y -> compare x.title y.title
            | _ -> invalidArg "yobj" "cannot compare values of different types"


and [<CustomEquality; CustomComparison>] TextAreaElement =
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


and [<CustomEquality; CustomComparison>] TabItemElement =
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
and [<CustomEquality; CustomComparison>] TreeItemElement =
    {   title: string
        elements: Element list
        onTreeItemSelected : (unit -> unit) option } 
    override x.Equals(yobj) =
        match yobj with
        | :? TreeItemElement as y -> x.title = y.title && x.elements = y.elements
        | _ -> false
 
    override x.GetHashCode() = (hash x.title) ^^^ (hash x.elements)
    interface System.IComparable with
        member x.CompareTo yobj =
            match yobj with
            | :? TreeItemElement as y -> compare x.title y.title
            | _ -> invalidArg "yobj" "cannot compare values of different types"
