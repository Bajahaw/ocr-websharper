namespace ocr_websharper

open WebSharper.JavaScript
open Microsoft.FSharp.Control
open WebSharper
open WebSharper.JavaScript.Promise
open WebSharper.UI.Storage
open WebSharper.UI.Templating
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Client.Doc
open WebSharper.UI.Html 

[<JavaScript>]
module Client =
    
    type IndexTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>
             
    let performOcrAsync (imageDataBase64: string) : Async<Result<string, string>> =
        async {
            // Prepare payload
            let prompt = "Extract ALL text from this image exactly as it appears. Preserve original formatting, line breaks, punctuation and special characters. Return ONLY the extracted text with NO additional commentary."
            let payload =
                $"""
                {{
                  "model": "meta-llama/llama-4-scout-17b-16e-instruct",
                  "messages": [
                    {{
                      "role": "system",
                      "content": "You are an OCR Expert, return only text with markdown format if needed"
                    }},
                    {{
                      "role": "user",
                      "content": [
                        {{ "type": "text", "text": "{prompt}" }},
                        {{ "type": "image_url",  "image_url": {{ "url": "{imageDataBase64}" }} }}
                      ]
                    }}
                  ]
                }}  
            """
            
            let url = "https://api.groq.com/openai/v1/chat/completions"
            let server = JS.Window.LocalStorage.GetItem("pong")
            let headers = Headers() // JS Headers instance
            headers.Append("Content-Type", "application/json")
            headers.Append("Authorization", server)
            let props = RequestOptions (
                  Method = "POST",
                  Headers = headers,
                  Body = payload
                )
            let! resp = JS.Fetch(url, props) |> AsAsync
            let! raw = resp.Text() |> AsAsync
            let parsed = JSON.Parse raw
            let result = parsed?choices?at(0)?message?content
            
            return Ok result
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
                    state.ImagePreviewUrl.Value <- None
                else
                    state.IsLoading.Value <- true
                    state.OcrResult.Value <- None
                    state.ErrorMessage.Value <- None
                    state.ImagePreviewUrl.Value <- None 

                    // Read file for preview
                    let! dataUrl = readFileAsDataURL file

                    // Show preview
                    state.ImagePreviewUrl.Value <- Some dataUrl

                    // performing the OCR function
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
        let bgImageStyle =
            state.ImagePreviewUrl.View
            |> View.Map (function
                | Some url -> sprintf "url('%s')" url
                | None     -> "")

        let placeholderDisplay =
            state.ImagePreviewUrl.View
            |> View.Map (function
                | Some _ -> "none"
                | None   -> "block")

        // --- UI Structure using WebSharper.UI and Bootstrap ---
        div [Attr.Class "container mt-4"] [
            h1 [Attr.Class "mb-4 text-center"] [text "AI Image OCR"]
            
            // needed for click-to-upload
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
                      match ev.JS.Self?dataTransfer?files |> Microsoft.FSharp.Collections.Array.tryHead with
                      | Some file -> handleFile file |> Microsoft.FSharp.Control.Async.Start
                      | None -> state.ErrorMessage.Value <- Some "No file dropped.");
                  on.paste (fun el ev ->
                      ev.PreventDefault()
                      match ev?clipboardData?files |> Microsoft.FSharp.Collections.Array.tryHead with
                      | Some files -> handleFile files |> Microsoft.FSharp.Control.Async.Start
                      | None -> state.ErrorMessage.Value <- Some "No file pasted."
                  )
                ]
                [
                  Doc.BindView (fun urlOpt ->
                    match urlOpt with
                    | Some url ->
                        img [ attr.src url
                              Attr.Class "img-fluid"
                              attr.style "max-height: 300px; display: block; margin: auto;" ] []
                    | None ->
                        Doc.Concat [
                          p [ Attr.Class "lead" ] [ text "Drop image here, paste, or click to upload" ]
                          i [ Attr.Class "fs-3 text-muted" ] [ text "(PNG, JPG, GIF, etc.)" ]
                        ]
                  ) state.ImagePreviewUrl.View
                ]
                

            // Loading Indicator
            div [ Attr.Class "text-center mt-3"
                  Attr.DynamicStyle "display" (state.IsLoading.View |> View.Map (function true -> "block" | false -> "none"))
                ]
                [ div [Attr.Class "spinner-border text-primary"; ] 
                      [ span [Attr.Class "visually-hidden"] [text "Loading..."] ]
                ]

            // Error Message Area
            Doc.BindView (fun errorMsg ->
                match errorMsg with
                | Some msg ->
                    div [Attr.Class "alert alert-danger mt-3";] 
                        [text msg]
                | None -> Doc.Empty)
                state.ErrorMessage.View

            // OCR Result Area
            Doc.BindView (fun ocrResult ->
                match ocrResult with
                | Some txt ->
                    div [Attr.Class "mt-4"] [
                        h4 [] [text "OCR Result:"]
                        pre [attr.id "ocr-result-text"] [text txt] 
                    ]
                | None -> Doc.Empty)
                state.OcrResult.View
        ]
        |> RunById "main"        

    [<SPAEntryPoint>]
    let EntryPoint() =
       Main()