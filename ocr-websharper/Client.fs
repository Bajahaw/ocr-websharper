namespace ocr_websharper

open System.Runtime.InteropServices.JavaScript
open Microsoft.AspNetCore.Components.Web
open System
open System.IO
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Control
open WebSharper
open WebSharper.Core.AST
open WebSharper.JavaScript
open WebSharper.JavaScript.Dom
open WebSharper.JavaScript.Promise
open WebSharper.UI.Templating
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Client.Doc
open WebSharper.UI.Html // Open Html module for easier access to Elt/Attr

[<JavaScript>]
module Client =
    
    // Placeholder for the actual AI API call
    // Takes image data (e.g., base64 string) and returns the extracted text
    
    type IndexTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>

    
    let performOcrAsync (imageDataBase64: string) : Async<Result<string, string>> =
        async {
            // Simulate network delay
            // do! Async. 2000 // Simulate 2 seconds processing time

            // **PLACEHOLDER LOGIC**
            // In a real application, you would make an HTTP request here:
            // - Use JS.Fetch or a WebSharper HTTP library.
            // - Send `imageDataBase64` (potentially without the `data:image/...;base64,` prefix)
            //   to your AI provider's API endpoint (Ollama, OpenAI, Groq, etc.).
            // - Handle the API response (success or error).
            // - Remember to handle CORS if calling directly from the browser.
            //   Often, it's better to proxy the call through your own backend.

            printfn "Simulating OCR for image data starting with: %s..." (imageDataBase64.Substring(0, 50))

            // Simulate a successful response
            let dummyResult = "This is the simulated OCR text extracted from the image.\nIt might contain multiple lines."
            return Ok dummyResult

            // Example of simulating an error:
            // return Error "Simulated AI API error: Could not process image."
        }

    // Helper to read a File object as a Base64 Data URL
    let readFileAsDataURL (file: File) : Async<string> =
        Microsoft.FSharp.Control.Async.FromContinuations(fun (cont, econt, _) ->
            let reader: FileReader<_> = JS.Inline("new FileReader()")
            reader.OnLoad <- fun (_: ProgressEvent) ->
                match reader.Result with
                | dataUrl -> cont dataUrl
                | _ -> econt (System.Exception("Failed to read file as Data URL"))
            reader.OnError <- fun (_: ProgressEvent) ->
                econt (System.Exception("Error reading file"))
            reader.ReadAsDataURL(file)
        )
        
    type State = {
            ImagePreviewUrl: Var<string option>
            OcrResult: Var<string option>
            IsLoading: Var<bool>
            ErrorMessage: Var<string option>
            IsDragOver: Var<bool> }

    // Main UI function
    let Main () =
    

        let state =
            { ImagePreviewUrl = Var.Create None
              OcrResult = Var.Create None
              IsLoading = Var.Create false
              ErrorMessage = Var.Create None
              IsDragOver = Var.Create false }

        // Function to handle the selected file
        let handleFile (file: File) =
            async {
                // Basic validation (check if it's an image)
                if not (file.Type.StartsWith("image/")) then
                    state.ErrorMessage.Value <- Some $"Invalid file type: {file.Type}. Please upload an image."
                    state.ImagePreviewUrl.Value <- None // Clear preview if invalid
                else
                    state.IsLoading.Value <- true
                    state.OcrResult.Value <- None
                    state.ErrorMessage.Value <- None
                    state.ImagePreviewUrl.Value <- None // Clear previous preview immediately

                    // Read file for preview
                    let! dataUrl = readFileAsDataURL file

                    // Show preview
                    state.ImagePreviewUrl.Value <- Some dataUrl

                    // Call the (placeholder) OCR function
                    let! result = performOcrAsync dataUrl

                    // Update UI based on result
                    match result with
                    | Ok ocrText ->
                        state.OcrResult.Value <- Some ocrText
                        state.ErrorMessage.Value <- None
                    | Error msg ->
                        state.ErrorMessage.Value <- Some msg
                        state.OcrResult.Value <- None // Clear previous result on error
                        state.IsLoading.Value <- false
            }

        // Hidden file input element reference
        
        let fileInputId = "file-input"
        let fileInput = input [attr.id fileInputId; attr.``type`` "file"; attr.accept "image/*"; attr.style "display: none;"] []

        // --- UI Structure using WebSharper.UI and Bootstrap ---
        div [Attr.Class "container mt-4"] [
            h1 [Attr.Class "mb-4 text-center"] [text "AI Image OCR"]
            
            // Hidden File Input (needed for click-to-upload)
            fileInput

            // Drop Zone Area
            div [ attr.id "drop-zone"
                  // Dynamic class for drag-over effect
                  Attr.DynamicClassPred "drag-over" state.IsDragOver.View
                  // Click handler: triggers the hidden file input
                  on.click (fun el _ -> JS.Document.GetElementById(fileInputId)?click());
                  // Drag and Drop handlers
                  on.dragOver (fun el ev ->
                      ev.PreventDefault() // Necessary to allow drop
                      state.IsDragOver.Value <- true);
                  on.dragLeave (fun el ev ->
                      ev.PreventDefault()
                      state.IsDragOver.Value <- false);
                  on.drop (fun el ev ->
                      ev.PreventDefault()
                      state.IsDragOver.Value <- false
                      match ev.JS.Self?dataTransfer?files |> Array.tryHead with
                      | Some file -> handleFile file |> Microsoft.FSharp.Control.Async.Start
                      | None -> state.ErrorMessage.Value <- Some "No file dropped.");
                  on.paste (fun el ev ->
                      ev.PreventDefault()
                      match ev?clipboardData?files |> Array.tryHead with
                      | Some files -> handleFile files |> Microsoft.FSharp.Control.Async.Start
                      | None -> state.ErrorMessage.Value <- Some "No file pasted."
                  )

                ]
                [ p [Attr.Class "lead"] [text "Drop image here, paste, or click to upload"]
                  i [Attr.Class "fs-3 text-muted"] [text "(PNG, JPG, GIF, etc.)"]
                ]
                

            // Loading Indicator
            div [ Attr.Class "text-center mt-3"
                  Attr.DynamicStyle "display" (state.IsLoading.View |> View.Map (function true -> "block" | false -> "none"))
                ]
                [ div [Attr.Class "spinner-border text-primary"; ] //attr.role "status"
                      [ span [Attr.Class "visually-hidden"] [text "Loading..."] ]
                ]

            // Error Message Area
            Doc.BindView (fun errorMsg ->
                match errorMsg with
                | Some msg ->
                    div [Attr.Class "alert alert-danger mt-3";] //Attr.Role "alert"
                        [text msg]
                | None -> Doc.Empty)
                state.ErrorMessage.View

            // Image Preview Area
            Doc.BindView (fun previewUrl ->
                match previewUrl with
                | Some url ->
                    div [Attr.Class "text-center mt-3"] [
                        img [attr.id "hello"] []
                    ]
                | None -> Doc.Empty)
                state.ImagePreviewUrl.View

            // OCR Result Area
            Doc.BindView (fun ocrResult ->
                match ocrResult with
                | Some txt ->
                    div [Attr.Class "mt-4"] [
                        h4 [] [text "OCR Result:"]
                        pre [attr.id "ocr-result-text"] [text txt] // Use <pre> for formatting
                    ]
                | None -> Doc.Empty)
                state.OcrResult.View
        ]
        |> RunById "main"        

    // Entry point for the client-side application
    [<SPAEntryPoint>]
    let EntryPoint() =
       Main()