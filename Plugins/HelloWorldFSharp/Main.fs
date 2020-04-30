namespace HelloWorldFSharp

open Flow.Launcher.Plugin
open System.Collections.Generic

type HelloWorldFSharpPlugin() =

    let mutable initContext = PluginInitContext()

    interface IPlugin with
        member this.Init (context: PluginInitContext) =
            initContext <- context

        member this.Query (query: Query) =
            List<Result> [
                Result (Title = "Hello World from F#",
                        SubTitle = sprintf "Query: %s" query.Search)

                Result (Title = "Browse source code of this plugin",
                        SubTitle = "click to open in browser",
                        Action = (fun ctx ->
                                    initContext.CurrentPluginMetadata.Website
                                        |> System.Diagnostics.Process.Start
                                        |> ignore
                                    true))

                Result (Title = "Trigger a tray message",
                        Action = (fun _ ->
                                    initContext.API.ShowMsg ("Sample tray message", "from the F# plugin")
                                    false))
            ]
