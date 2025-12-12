"""
OPTION B: Structured JSON Response with Prompt Engineering
This is the upgrade from Option A. Use this after testing basic LLM connectivity.

To upgrade:
1. Stop llm_server.py
2. Replace contents with this file
3. Restart server
4. Test with: "spawn a red chair"
"""

from flask import Flask, request, jsonify
from flask_cors import CORS
from openai import OpenAI
import json
import re

app = Flask(__name__)
CORS(app)

# Initialize OpenAI client pointing to Jan AI
client = OpenAI(
    base_url="http://127.0.0.1:1337/v1",
    api_key="daniellocal"
)

# System prompt for structured JSON responses
SYSTEM_PROMPT = """You are an AI assistant for a VR furniture placement system.
Users will give voice commands to spawn, delete, or modify objects.

CRITICAL: Always respond with ONLY valid JSON. No extra text before or after.

JSON Format:
{
  "action": "spawn|delete|switch_mode|rotate|scale|query",
  "objectName": "chair|table|lamp|sofa|bed|desk|shelf|etc",
  "category": "chair|table|lamp|etc",
  "color": "red|blue|green|white|black|brown|etc or null",
  "quantity": 1,
  "interactionMode": "ray|direct|null",
  "scale": 1.0,
  "response": "optional conversational text"
}

EXAMPLES:

User: "spawn a chair"
{"action":"spawn","objectName":"chair","category":"chair","color":null,"quantity":1}

User: "create a red table"
{"action":"spawn","objectName":"table","category":"table","color":"red","quantity":1}

User: "delete that"
{"action":"delete"}

User: "switch to direct mode"
{"action":"switch_mode","interactionMode":"direct"}

User: "spawn 3 blue chairs"
{"action":"spawn","objectName":"chair","category":"chair","color":"blue","quantity":3}

User: "what can you do?"
{"action":"query","response":"I can spawn furniture like chairs, tables, lamps, sofas, beds, and more. I can also delete objects and switch interaction modes."}

IMPORTANT RULES:
1. ONLY return JSON, nothing else
2. If unsure, use action:"query" and explain in "response"
3. Common furniture: chair, table, lamp, sofa, bed, desk, shelf, cabinet, bench, stool
4. Available colors: red, blue, green, white, black, brown, gray, yellow, orange, purple
5. Keep objectName lowercase and singular
"""

@app.route('/ping', methods=['GET'])
def ping():
    """Health check endpoint"""
    return jsonify({"status": "ok", "message": "LLM server running (Option B - JSON mode)"}), 200

@app.route('/api/process_command', methods=['POST'])
def process_command():
    """
    OPTION B: Structured JSON response
    Receives voice command, sends to LLM with system prompt, returns parsed JSON
    """
    data = request.get_json()
    
    if not data or 'command' not in data:
        return jsonify({"error": "No command provided"}), 400
    
    user_command = data.get('command', '')
    print(f"\n[LLM Server] Received: {user_command}")
    
    try:
        # Send to Jan AI with structured system prompt
        response = client.chat.completions.create(
            model="Jan-v1-4B-Q4_K_M",
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": user_command}
            ],
            temperature=0.3,  # Low temperature for more deterministic output
            max_tokens=200
        )
        
        llm_text = response.choices[0].message.content.strip()
        print(f"[LLM Server] Raw response: {llm_text}")
        
        # Extract JSON from response (LLM might add extra text)
        json_match = re.search(r'\{.*\}', llm_text, re.DOTALL)
        
        if json_match:
            json_str = json_match.group(0)
            result = json.loads(json_str)
            
            # Validate and add defaults
            result.setdefault('action', 'query')
            result.setdefault('quantity', 1)
            result.setdefault('scale', 1.0)
            
            print(f"[LLM Server] Parsed JSON: {result}")
            return jsonify(result), 200
        else:
            # Fallback: treat as conversational query
            print(f"[LLM Server] No JSON found, treating as query")
            return jsonify({
                "action": "query",
                "response": llm_text,
                "command": user_command
            }), 200
        
    except json.JSONDecodeError as e:
        print(f"[LLM Server] JSON parse error: {e}")
        return jsonify({
            "action": "query",
            "response": llm_text,
            "error": "Invalid JSON from LLM"
        }), 200
        
    except Exception as e:
        error_msg = f"LLM Error: {str(e)}"
        print(f"[LLM Server] {error_msg}")
        return jsonify({"error": error_msg}), 500

if __name__ == '__main__':
    print("\n" + "="*60)
    print("LLM Server Starting - Option B (Structured JSON)")
    print("="*60)
    print(f"Server URL: http://localhost:5000")
    print(f"Jan AI URL: http://127.0.0.1:1337/v1")
    print(f"Model: Jan-v1-4B-Q4_K_M")
    print("\nFeatures:")
    print("  - Structured JSON responses")
    print("  - Object spawning with colors")
    print("  - Quantity support")
    print("  - Interaction mode switching")
    print("  - Delete commands")
    print("\nEndpoints:")
    print("  GET  /ping - Health check")
    print("  POST /api/process_command - Process voice commands")
    print("="*60 + "\n")
    
    app.run(host='0.0.0.0', port=5000, debug=True)
