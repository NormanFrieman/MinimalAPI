open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Npgsql
open Dapper.FSharp.PostgreSQL
open System.Collections

OptionTypes.register()
type Client = { id: Guid; name: string; email: string }
let clientTable =
    table'<Client> "client" |> inSchema "public"

let connString = "Host=localhost;Username=postgres;Password=mysecretpassword;Database=postgres"

let getClients (): IEnumerable =
    let conn = new NpgsqlConnection(connString)
    let clients =
        select {
            for c in clientTable do selectAll
        } |> conn.SelectAsync<Client>

    clients.Result

let createClient name email =
    let id = Guid.NewGuid()
    let client = { id = id; name = name; email = email }
    
    let conn = new NpgsqlConnection(connString)
    insert {
        into clientTable
        value client
    } |> conn.InsertAsync<Client> |> ignore

    id

let deleteClient id =
    let conn = new NpgsqlConnection(connString)
    let result =
        (delete {
            for c in clientTable do
            where (c.id = id)
        } |> conn.DeleteAsync).Result
    if result = 1 then "Client removed" else "Client not found"

let updateClient id name email =
    let conn = new NpgsqlConnection(connString)
    let resultList =
        List.ofSeq((select {
            for c in clientTable do
            where (c.id = id)
        } |> conn.SelectAsync<Client>).Result)

    if resultList.Length > 0 then
        let clientOld = resultList.Head

        let newName = if String.IsNullOrEmpty(name) then clientOld.name else name
        let newEmail = if String.IsNullOrEmpty(email) then clientOld.email else email

        let client = { clientOld with name = newName; email = newEmail }
        update {
            for c in clientTable do
            set client
            where (c.id = id)
        } |> conn.UpdateAsync |> ignore

        "Updated client"
    else
        "Client not found"
    

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    let app = builder.Build()

    app.MapGet("/client",
        Func<IEnumerable>(getClients)) |> ignore

    app.MapPost("/client",
        Func<{| name: string; email: string |}, Guid>(fun req -> (createClient req.name req.email))) |> ignore

    app.MapDelete("/client/{id}",
        Func<Guid, string>(fun id -> (deleteClient id))) |> ignore

    app.MapPut("/client/{id}",
        Func<Guid, {| name: string; email: string|}, string>(fun id req -> (updateClient id req.name req.email))) |> ignore

    app.Run()

    0