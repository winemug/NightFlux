module Calculations
open NightFlux
open Deedle
open System
open FSharp.Charting

//let InsulinOnSite start stop = 
//    let ds = NightFluxSeries.GetSeries(start, stop)    
//    let df = Frame.ofRows ds.ValuesAll

//    return df



    
//let DoStuff =
//    printfn "doing stuff"
//    let startDate = DateTimeOffset.UtcNow.AddHours -6.
//    let endDate = DateTimeOffset.UtcNow
//    let mySeries = InsulinOnSite startDate endDate

//    printfn "did stuff"

//[<EntryPoint>]
//let main argv =
//    DoStuff
//    Console.Read() |> ignore
//    0