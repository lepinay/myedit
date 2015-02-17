open System.Management.Automation.Runspaces
open System.Management.Automation
open System

// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

[<EntryPoint>]
let main argv = 
    let myRunSpace = RunspaceFactory.CreateRunspace();
    myRunSpace.Open();
    use powershell = PowerShell.Create();
    powershell.Runspace <- myRunSpace;
    powershell.AddScript("elm-make.exe C:\Users\Laurent\Documents\code\like\main.elm --yes") |> ignore
    powershell.AddCommand("out-default") |> ignore
    powershell.Commands.Commands.[0].MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);
    let results = powershell.Invoke()
    if powershell.Streams.Error.Count > 0 then
        for err in powershell.Streams.Error do
            printfn "%s" err.ErrorDetails.Message
        powershell.Streams.Error.Clear();
    else
        for item in results do
            printfn "%s" <| item.ToString()

    printfn "%A" argv
    Console.ReadKey() |> ignore
    0 // return an integer exit code
