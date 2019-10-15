namespace NightLight

open System
open Deedle

module Transforms =

    let Compose (bg_dict) =
        let bgSeries = Series (bg_dict)
        0
