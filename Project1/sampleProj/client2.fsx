#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"
#r "nuget: Akka"
open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp 
open Akka.Remote
// the most basic configuration of remote actor system
let config = """
akka {
    actor {
        provider = "Akka.Remote.RemoteActorRefProvider, Akka.Remote"
    }
    remote.helios.tcp {
        transport-protocol = tcp
        port = 8080                    # get first available port
        hostname = 192.168.0.96
    }
}
"""

// create remote deployment configuration for actor system available under `systemPath`
let remoteDeploy systemPath =
    let add =
        match ActorPath.TryParseAddress systemPath with
        | false, _ -> failwith "ActorPath address cannot be parsed"
        | true, a -> a
    Deploy(RemoteScope(add))

[<Literal>]
let REQ = 1

[<Literal>]
let RES = 2

[<EntryPoint>]
let main _ =

    System.Console.Title <- "Local: " + System.Diagnostics.Process.GetCurrentProcess().Id.ToString()

    // remote system address according to settings provided
    // in FSharp.Deploy.Remote configuration
    let remoteSystemAddress = "akka.tcp://remote-system@localhost:7000"
    use system = System.create "local-system" (Configuration.parse config)

    // spawn actor remotelly on remote-system location
    let remoter =
        // as long as actor receive logic is serializable F# Expr, there is no need for sharing any assemblies
        // all code will be serialized, deployed to remote system and there compiled and executed
        spawne system "remote"
            <@
                fun mailbox ->
                let rec loop(): Cont<int * string, unit> =
                    actor {
                        let! msg = mailbox.Receive()
                        match msg with
                        | (REQ, m) ->
                            printfn "Remote actor received: %A" m
                            mailbox.Sender() <! (RES, "ECHO " + m)
                        | _ -> logErrorf mailbox "Received unexpected message: %A" msg
                        return! loop()
                    }
                loop()
             @> [ SpawnOption.Deploy(remoteDeploy remoteSystemAddress) ] // Remote options

    while true do
        System.Console.Write("Type the message to send :" )
        let message = System.Console.ReadLine()
        async {
            let! (msg: int * string) = remoter <? (REQ, message)
            match msg with
            | (RES, m) -> printfn "Remote actor responded: %s" m
            | _ -> printfn "Unexpected response from remote actor"
        }
        |> Async.RunSynchronously

    System.Console.ReadLine() |> ignore
    0