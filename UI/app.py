"""Streamlit UI for EmailSafeAISearch. Calls the .NET API; contains no PII logic itself."""

import json
import os
from pathlib import Path

import requests
import streamlit as st

API_URL = os.environ.get("API_URL", "http://localhost:5298")
TASKS_PATH = Path(__file__).resolve().parent.parent / "EmailSafeAISearch" / "Tests" / "data" / "tasks.json"
OWN_QUESTION = "(type your own)"


@st.cache_data
def load_sample_questions() -> list[str]:
    try:
        return [task["question"] for task in json.loads(TASKS_PATH.read_text())]
    except (OSError, json.JSONDecodeError, KeyError):
        return []


st.set_page_config(page_title="Private Email Q&A", layout="wide")
st.title("Ask Your Emails, Privately")
st.markdown(
    "Ask a question about a set of 30 sample company emails. The app finds the relevant emails "
    "and asks an AI (Gemini) to answer. **Names, phone numbers, email addresses and other private "
    "details are replaced with placeholders before anything is sent to the AI**, then put back in the final answer."
)

with st.expander("How it works"):
    st.markdown(
        "1. Your question is checked for private details, which are hidden.\n"
        "2. The emails most related to your question are found (all emails are already hidden).\n"
        "3. Only the hidden text is sent to Gemini.\n"
        "4. The real values are restored in Gemini's answer.\n\n"
        "The API must be running at `" + API_URL + "` and Presidio must be running in Docker."
    )

samples = load_sample_questions()
st.markdown("#### Step 1: Pick a sample question, or type your own")
choice = st.selectbox(
    "Sample questions",
    [OWN_QUESTION, *samples],
    help="These come from the project's test questions (tasks.json), which have known correct answers.",
)
if not samples:
    st.caption(f"No sample questions found at {TASKS_PATH}")

question = st.text_input(
    "Your question about the emails",
    value="" if choice == OWN_QUESTION else choice,
    placeholder="e.g. When will order INV-20391 arrive?",
)

st.markdown("#### Step 2: Get the answer")
if st.button("Ask the AI", type="primary", disabled=not question.strip()):
    with st.spinner("Searching emails and asking the AI..."):
        try:
            response = requests.post(f"{API_URL}/api/ask", json={"question": question}, timeout=120)
            response.raise_for_status()
            result = response.json()
        except requests.RequestException as error:
            st.error(f"API call failed: {error}")
            st.stop()

    st.subheader("Answer")
    st.success(result["final"])
    if result["unknownPlaceholders"]:
        st.warning(f"The AI returned placeholders that do not exist: {', '.join(result['unknownPlaceholders'])}")

    col1, col2, col3 = st.columns(3)

    with col1:
        st.markdown("**1. Your question**")
        st.write(question)
        st.markdown("**Emails found**")
        st.write(", ".join(f"#{email_id}" for email_id in result["retrievedEmailIds"]))

    with col2:
        st.markdown("**2. Sent to AI** (private data hidden)")
        st.text_area("sent", result["sentToAi"], height=400, disabled=True, label_visibility="collapsed")

    with col3:
        st.markdown("**3. AI answer** (before restoring)")
        st.text_area("ai", result["aiAnswer"], height=150, disabled=True, label_visibility="collapsed")
        st.markdown("**Placeholder map** (never sent to the AI)")
        st.json(result["map"], expanded=False)