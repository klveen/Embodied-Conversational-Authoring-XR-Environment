import webbrowser
import os
from threading import Timer
from flask import Flask, request, jsonify
from openai import OpenAI

# Serve files from the static directory
app = Flask(__name__, static_url_path='', static_folder='static')

# Initialize the OpenAI client
client = OpenAI(
    base_url="http://127.0.0.1:1337/v1",
    api_key="daniellocal"
)

@app.route('/prompt', methods=['POST'])
def handle_prompt():
    """Handle a prompt from the user and return a response."""
    data = request.get_json()
    prompt = data.get('prompt', '')

    try:
        response = client.chat.completions.create(
            model="Jan-v1-4B-Q4_K_M",
            messages=[
                {"role": "user", "content": prompt}
            ]
        )
        response_text = response.choices[0].message.content
    except Exception as e:
        print(f"Error calling OpenAI API: {e}")
        response_text = "Error: Could not get a response from the local LLM."

    print(f"LLM Response: {response_text}")
    return jsonify({'response': response_text})

def open_browser():
    """Open the default web browser to the index page."""
    webbrowser.open_new('http://127.0.0.1:5000/')

if __name__ == '__main__':
    if not os.environ.get("WERKZEUG_RUN_MAIN"):
        Timer(1, open_browser).start()
    app.run(port=5000, debug=True)
