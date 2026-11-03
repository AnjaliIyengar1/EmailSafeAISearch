# EmailSafeAISearch

## The Problem

Companies want to use AI to search and answer questions about their emails, but emails are full of private data: names, phone numbers, email addresses, home addresses, card numbers. Sending them to an external AI service risks leaking that data.

## The Solution

Ask questions about a set of emails without the AI ever seeing private data:

1. **Hide:** private details are found (with Microsoft Presidio, running locally) and replaced with placeholders like `<PERSON_1>`.
2. **Search (RAG):** the emails most related to the question are found.
3. **Ask:** only the hidden text is sent to the AI (Gemini).
4. **Restore:** the real values are put back into the AI's answer before the user sees it.

```
Actual email: "Call Priya at 408-555-0142"  ->  AI sees: "Call <PERSON_2> at <PHONE_NUMBER_1>"  ->  user sees: "Call Priya at 408-555-0142"
```

## Architecture

```
UI / curl
   |
   v
YOUR API  ([ApiController], localhost:5298)
   |
   |--> 1. Presidio  (localhost:5002, Docker)  "find the PII"
   |
   |--> 2. Gemini    (Google, internet)        "answer the question"
```

| API | Built by | Runs where | Called from |
|---|---|---|---|
| EmailSafeAISearch API | This project | Local machine | UI or curl |
| Presidio Analyzer | Microsoft | Local machine (Docker) | `PresidioClient` |
| Gemini | Google | Internet | `GeminiClient` |

Presidio runs locally, so emails with real PII never leave the machine. Only text with the PII already hidden is sent to Gemini.

### How does `GeminiClient` see the email?

It doesn't need to know anything about emails. `AskService` builds one string containing the instructions, the hidden email and the hidden question, and passes that string to `GeminiClient`. `GeminiClient` just sends whatever text it's given to Google.

```
AskService:   prompt = Instructions + "Email: Hi <PERSON_1>..." + "Question: Who should <PERSON_1> call?"
                 |
GeminiClient: sends the prompt string to Gemini, returns the reply
```

## Project Structure

```
EmailSafeAISearch/
├── docker-compose.yml           Starts Presidio in Docker
├── UI/                          Streamlit web page (Python); calls the API, has no PII logic
│   ├── app.py                   The page: question input, answer, and what was sent to the AI
│   └── requirements.txt         Python packages the UI needs
└── EmailSafeAISearch/           The Web API
    ├── Program.cs               Registers services and starts the API
    ├── appsettings.json         Settings: Presidio URL, Gemini models, RAG options
    ├── Controllers/
    │   ├── AskController.cs     POST api/ask: main endpoint, answers a question about the emails
    │   └── PiiController.cs     POST api/pii/detect|hide|restore: test endpoints for PII handling
    ├── Services/
    │   ├── PresidioClient.cs    Asks Presidio where the PII is
    │   ├── PiiService.cs        Replaces PII with placeholders and restores it
    │   ├── EmailIndex.cs        Hides and embeds all emails once; finds the closest ones to a question
    │   ├── GeminiEmbeddingClient.cs  Turns hidden text into vectors for search
    │   ├── GeminiClient.cs      Sends the hidden prompt to Gemini, returns the answer
    │   └── AskService.cs        Full flow: hide -> search -> ask Gemini -> restore
    ├── Models/                  Data shapes (PII results, emails, API responses)
    ├── data/emails.json         30 fake emails the app searches, with an answer key for tests
    └── Tests/data/tasks.json    10 test questions with expected answers
```

Interfaces (`IPiiDetector`, `IPiiService`, `ILlmClient`, `IEmbeddingClient`) let tests swap in fakes for Presidio and Gemini.

## Running Locally

Prerequisites: [Docker Desktop](https://www.docker.com/products/docker-desktop/), the .NET SDK, Python 3, and a Gemini API key from Google AI Studio (never commit the key).

Open the repo root folder in VS Code. Three things run at the same time: Presidio (Docker), the API (Terminal 1) and the UI (Terminal 2). Open a new terminal with **Terminal → New Terminal**.

**1. Start Presidio.** Open Docker Desktop, then from the repo root:

```bash
docker compose up -d
```

`docker-compose.yml` defines which containers to run. `docker compose up -d` downloads the Presidio image if needed and starts it in the background. This runs the Presidio Analyzer in a container at `http://localhost:5002`. The API talks to it over HTTP like any other REST API. The first run downloads the image, which takes a few minutes.

**2. Terminal 1: run the API** (leave it running):

```bash
export Gemini__ApiKey="YOUR_KEY"
dotnet run --project EmailSafeAISearch
```

Wait for `Now listening on: http://localhost:5298`.

**3. Terminal 2: run the UI** (leave it running):

```bash
cd UI
python3 -m venv .venv                 # first time only
source .venv/bin/activate             # every new terminal
pip install -r requirements.txt       # first time only
streamlit run app.py
```

The UI opens at `http://localhost:8501`.

**Optional: test the API without the UI** (`-i` prints the HTTP status, which helps when something fails):

```bash
# PII detection only
curl -i -X POST http://localhost:5298/api/pii/detect \
  -H "Content-Type: application/json" \
  -d '{"text": "Hi Mark, call Priya at 408-555-0142"}'

# Full flow: search emails, ask Gemini, restore PII
curl -i -X POST http://localhost:5298/api/ask \
  -H "Content-Type: application/json" \
  -d '{"question": "When will order INV-20391 arrive?"}'
```

**To stop everything:** `Ctrl+C` in Terminals 1 and 2, then `docker compose down` (stops and removes the Presidio container; the downloaded image is kept, so the next start is fast).

## Test Data

`emails.json` is in `EmailSafeAISearch/data/` and `tasks.json` is in `EmailSafeAISearch/Tests/data/`. All names, emails, phone numbers and addresses are fake. Emails use the reserved `example.com` domain and phone numbers use the reserved `555-01xx` range.

### `emails.json`: 30 fake emails with an answer key

Each entry looks like this:

```json
{
  "id": 1,
  "thread": "march-order",
  "raw_email": "From: priya.shah@example.com\nTo: mark.lopez@example.com\nSubject: Delivery date for the March order\n\nHi Mark,\n\nCan you confirm the delivery date for order INV-20391? The client wants it before March 15.\n\nThanks,\nPriya Shah\n(408) 555-0142",
  "expected_pii": [
    { "type": "EMAIL_ADDRESS", "value": "priya.shah@example.com" },
    { "type": "EMAIL_ADDRESS", "value": "mark.lopez@example.com" },
    { "type": "PERSON", "value": "Mark" },
    { "type": "PERSON", "value": "Priya Shah" },
    { "type": "PHONE_NUMBER", "value": "(408) 555-0142" }
  ],
  "not_pii": ["INV-20391", "March 15"]
}
```

Only `raw_email` is sent to the app. `expected_pii` and `not_pii` are the answer key, used by the tests to check what the app found.

| Field | Used by | Purpose |
|---|---|---|
| `raw_email` | The app | The only input. PII detection must find the PII on its own. |
| `expected_pii` | Tests | PII that must be hidden before the text reaches the AI |
| `not_pii` | Tests | Values that look sensitive but must **not** be hidden (order numbers, dates) |
| `thread` | Tests | Groups related emails, so search (RAG) can be tested later |

### `tasks.json`: 10 AI test questions with expected answers

Each task sends selected emails and a question to the AI, then checks the final answer after the PII is restored.

| Field | Purpose |
|---|---|
| `task` | `summarize`, `extract` or `qa` |
| `question` | What is asked of the AI |
| `relevant_email_ids` | Which emails to use |
| `expected_facts` | Text the final answer must contain |
| `must_not_contain` | Text the final answer must not contain (leftover placeholders, full card numbers) |
