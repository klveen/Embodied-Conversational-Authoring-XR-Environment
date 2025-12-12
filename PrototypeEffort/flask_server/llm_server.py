"""
Simple LLM Server - Option A: Text Response Testing
Integrates with Jan AI running locally for basic text responses.
Once tested, this will be upgraded to Option B with JSON responses.
"""

from flask import Flask, request, jsonify
from flask_cors import CORS
from openai import OpenAI

app = Flask(__name__)
CORS(app)  # Enable CORS for Unity WebGL if needed

# Initialize OpenAI client pointing to Jan AI local server
# (Matches supervisor's working code)
client = OpenAI(
    base_url="http://127.0.0.1:1337/v1",
    api_key="kayalocal"
)

@app.route('/ping', methods=['GET'])
def ping():
    """Health check endpoint"""
    return jsonify({"status": "ok", "message": "LLM server is running"}), 200

@app.route('/api/process_command', methods=['POST'])
def process_command():
    """
    OPTION A: Simple text response for testing
    Uses supervisor's approach with chat.completions.create()
    """
    data = request.get_json()
    
    if not data or 'command' not in data:
        return jsonify({"error": "No command provided"}), 400
    
    user_command = data.get('command', '')
    print(f"\n[LLM Server] Received command: {user_command}")
    
    try:
        print(f"[LLM Server] Connecting to Jan AI...")
        
        # Supervisor's approach - simple chat completion
        response = client.chat.completions.create(
            model="Jan-v1-4B-Q4_K_M",
            messages=[
                {"role": "user", "content": user_command}
            ]
        )
        
        llm_response = response.choices[0].message.content
        print(f"[LLM Server] ✓ Response: {llm_response}")
        
        return jsonify({
            "response": llm_response,
            "command": user_command
        }), 200
        
    except Exception as e:
        error_msg = f"LLM Error: {str(e)}"
        print(f"[LLM Server] ✗ {error_msg}")
        return jsonify({"error": error_msg, "response": f"Error: {str(e)}"}), 200

if __name__ == '__main__':
    print("\n" + "="*60)
    print("LLM Server Starting - Option A (Text Response Testing)")
    print("="*60)
    print(f"Server URL: http://localhost:5000")
    print(f"Jan AI URL: http://127.0.0.1:1337")
    print(f"Model: Jan-v1-4B-Q4_K_M")
    print("\nEndpoints:")
    print("  GET  /ping - Health check")
    print("  POST /api/process_command - Process voice commands")
    print("="*60 + "\n")
    
    app.run(host='0.0.0.0', port=5000, debug=True)
