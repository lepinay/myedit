open System
open System.IO
 
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
 
if not (File.Exists "paket.exe") then
    let url = "https://github.com/fsprojects/Paket/releases/download/0.26.3/paket.exe"
    use wc = new Net.WebClient() in let tmp = Path.GetTempFileName() in wc.DownloadFile(url, tmp); File.Move(tmp,Path.GetFileName url)
 
 
#r "paket.exe"
 
Paket.Dependencies.Install """
    source https://nuget.org/api/v2
    nuget FsUnit
    nuget FsUnit.Xunit
    nuget XUnit
    nuget FsCheck
""";;
 
 
#r @"packages\xunit\lib\net20\Xunit.dll"
#r @"packages\FsCheck\lib\net45\FsCheck.dll"
#r @"packages\FsUnit.xUnit\Lib\net40\NHamcrest.dll"
#r @"packages\FsUnit.xUnit\Lib\net40\FsUnit.CustomMatchers.dll"
#r @"packages\FsUnit.xUnit\Lib\net40\FsUnit.Xunit.dll"

open FsUnit.Xunit
open FsCheck

let explode (s:string) =  
    if s <> null then [for c in s -> c]
    else []

let findMatch s pos = 
    let rec _findMatch s curr pos opening (closing,dir) = 
        match s with
            | x::xs when x = closing && curr = 1 -> Some (pos)
            | x::xs when x = closing -> _findMatch xs (curr-1) (pos+dir) opening (closing,dir)
            | x::xs when x = opening -> _findMatch xs (curr+1) (pos+dir) opening (closing,dir)
            | x::xs ->  _findMatch xs (curr) (pos+dir) opening (closing,dir)
            | [] -> None
    if Seq.isEmpty s then None
    else 
        let closer = function
                | '(' -> (')',1)
                | '{' -> ('}',1)
                | '[' -> (']',1)
                | ')' -> ('(',-1)
                | '}' -> ('{',-1)
                | ']' -> ('[',-1)
                | other -> failwithf "Unreognized closing for %A" other
        let skipped = (Seq.skip pos s |> Seq.toList)
        match skipped with
            | x::xs when not (Array.exists (fun i -> i = x) [|'(';'{';'[';']';'}';')'|])  -> None
            | [] -> None
            | x::xs -> 
                match _findMatch skipped 0 0 x (closer x) with
                    | Some(n) -> Some (n + pos)
                    | None -> None
 
findMatch (explode "()") 0 |> should equal (Some 1)
findMatch (explode "(())") 0 |> should equal (Some 3)
findMatch (explode "(stuff())") 0 |> should equal (Some 8)
findMatch (explode "(()") 0 |> should equal None
findMatch (explode "(())") 1 |> should equal (Some 2)
findMatch (explode ")(") 1 |> should equal None
findMatch (explode "blabla()") 0 |> should equal None
findMatch (explode "()") 1 |> should equal (Some 0)


let willNeverFindMatchInEmptyList (e:int) = findMatch (explode "") e = None
Check.Quick willNeverFindMatchInEmptyList

type SymParens = SymParens of char seq with
  static member op_Explicit(SymParens s) = s

type MyGenerators =
  static member SymParens() =
      {new Arbitrary<SymParens>() with
          override x.Generator = gen {
                let! i = Gen.choose(1,100)
                let a = List.replicate i '('
                let b = List.replicate i ')'
                return SymParens (Seq.concat [a;b])
            }
          override x.Shrinker t = Seq.empty }
        
Arb.register<MyGenerators>()


let willAlwaysFindParensInTheEnd (SymParens e) = 
    printfn "input length %d" (Seq.length e)
    findMatch e 0 = Some(Seq.length e - 1)
Check.Quick willAlwaysFindParensInTheEnd
