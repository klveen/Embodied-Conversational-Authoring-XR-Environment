"""
LLM Server - Claude Integration with Function Calling
Uses Claude's native tool calling for reliable structured outputs.
Fallback to Jan AI for offline testing.
"""

from flask import Flask, request, jsonify
from flask_cors import CORS
import json
import re
import os
from anthropic import Anthropic

# Model selection
MODEL_NAME = "claude-sonnet-4-0"  # Tested and working in Jan AI

# Your working API key
ANTHROPIC_API_KEY = "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"

app = Flask(__name__)
CORS(app)  # Enable CORS for Unity WebGL if needed

# Direct Anthropic client (bypass Jan AI)
client = Anthropic(api_key=ANTHROPIC_API_KEY)
print(f"🤖 Using Claude directly (bypassing Jan AI): {MODEL_NAME}")

# Detect which model type we're using
is_local_model = "Jan" in MODEL_NAME or "4B" in MODEL_NAME
is_claude = "claude" in MODEL_NAME.lower()

if is_claude:
    print(f"🤖 Using Claude through Jan AI proxy: {MODEL_NAME}")
elif is_local_model:
    print(f"🤖 Using local Jan AI model: {MODEL_NAME}")
else:
    print(f"🤖 Using external model through Jan AI: {MODEL_NAME}")

# Ultra-simple prompt that works with Jan AI's small model
SYSTEM_PROMPT2 = """Convert voice commands to JSON.

spawn/create/place + [color] + [object] = {"action":"spawn","objectName":"object","color":"color"}
delete/remove = {"action":"delete"}

Objects: chair, sofa, couch, table, lamp, bed, desk, shelf, bench, stool
Colors: red, blue, green, yellow, white, black, brown, orange, purple, pink, gray

Examples:
"spawn a red chair" → {"action":"spawn","objectName":"chair","color":"red"}
"blue sofa" → {"action":"spawn","objectName":"sofa","color":"blue"}
"place table" → {"action":"spawn","objectName":"table"}
"delete" → {"action":"delete"}

Only output valid JSON."""

# System prompt for structured JSON responses with more detailed rules

SYSTEM_PROMPT = """red chair → {"action":"spawn","objectName":"chair","color":"red"}
blue chair → {"action":"spawn","objectName":"chair","color":"blue"}
green table → {"action":"spawn","objectName":"table","color":"green"}
chair → {"action":"spawn","objectName":"chair"}
delete → {"action":"delete"}
make this blue → {"action":"modify","color":"blue"}"""

# CLAUDE FUNCTION DEFINITIONS
# These define the "tools" that Claude can use - like API endpoints it can call
# Each tool is like a structured form that Claude fills out
CLAUDE_TOOLS = [
    {
        "name": "spawn_furniture",
        "description": "Spawn a piece of furniture in the AR environment. Use this when the user wants to create, place, spawn, or add furniture.",
        "input_schema": {
            "type": "object",
            "properties": {
                "objectName": {
                    "type": "string",
                    "enum": ["chair", "table", "sofa", "couch", "lamp", "bed", "desk", "shelf", "bench", "stool"],
                    "description": "The type of furniture object to spawn"
                },
                "color": {
                    "type": "string",
                    "enum": ["red", "blue", "green", "yellow", "white", "black", "brown", "orange", "purple", "pink", "gray"],
                    "description": "Optional: The color to apply to the furniture. Only include if user specifies a color."
                },
                "quantity": {
                    "type": "integer",
                    "description": "Number of objects to spawn. Default is 1.",
                    "default": 1
                }
            },
            "required": ["objectName"]  # Only objectName is required, color is optional
        }
    },
    {
        "name": "delete_furniture",
        "description": "Delete the furniture object that the user is currently pointing at with their VR controller. Use this when user says delete, remove, get rid of, etc.",
        "input_schema": {
            "type": "object",
            "properties": {},  # No parameters needed - deletes whatever user is pointing at
            "required": []
        }
    },
    {
        "name": "modify_furniture",
        "description": "Modify the furniture object that the user is currently pointing at. Can change color or spawn a different variation of the same object type.",
        "input_schema": {
            "type": "object",
            "properties": {
                "color": {
                    "type": "string",
                    "enum": ["red", "blue", "green", "yellow", "white", "black", "brown", "orange", "purple", "pink", "gray"],
                    "description": "Optional: New color to apply. Include only if user wants to change color (e.g. 'make it blue')."
                },
                "variation": {
                    "type": "boolean",
                    "description": "Set to true if user wants a different model/variation (e.g. 'different chair', 'another one'). Default false.",
                    "default": False
                }
            },
            "required": []  # Both parameters are optional
        }
    }
]


@app.route('/ping', methods=['GET'])
def ping():
    """Health check endpoint"""
    return jsonify({"status": "ok", "message": "LLM server is running (JSON mode)"}), 200

@app.route('/api/process_command', methods=['POST'])
def process_command():
    """
    Process voice commands using either Claude (function calling) or Jan AI (pattern matching)
    """
    data = request.get_json()
    
    if not data or 'command' not in data:
        return jsonify({"error": "No command provided"}), 400
    
    user_command = data.get('command', '')
    print(f"\n[LLM Server] Received: {user_command}")
    
    try:
        if is_claude:
            # === CLAUDE APPROACH: Native Tool Use ===
            # Direct call to Anthropic API with native tool format
            
            response = client.messages.create(
                model=MODEL_NAME,
                max_tokens=200,
                tools=[
                    {
                        "name": "spawn_furniture",
                        "description": "Spawn furniture in AR space. Use when user wants to create/place/spawn furniture.",
                        "input_schema": {
                            "type": "object",
                            "properties": {
                                "objectName": {
                                    "type": "string",
                                    "enum": ["chair", "table", "sofa", "couch", "lamp", "bed", "desk", "shelf"],
                                    "description": "Type of furniture"
                                },
                                "color": {
                                    "type": "string",
                                    "enum": ["red", "blue", "green", "yellow", "white", "black", "brown", "orange", "purple", "pink", "gray"],
                                    "description": "Optional color"
                                },
                                "quantity": {
                                    "type": "integer",
                                    "description": "Number to spawn (default 1)"
                                }
                            },
                            "required": ["objectName"]
                        }
                    },
                    {
                        "name": "delete_furniture",
                        "description": "Delete the furniture object user is pointing at",
                        "input_schema": {
                            "type": "object",
                            "properties": {}
                        }
                    },
                    {
                        "name": "modify_furniture",
                        "description": "Modify the furniture object user is pointing at",
                        "input_schema": {
                            "type": "object",
                            "properties": {
                                "color": {
                                    "type": "string",
                                    "enum": ["red", "blue", "green", "yellow", "white", "black", "brown"],
                                    "description": "New color to apply"
                                }
                            }
                        }
                    }
                ],
                messages=[{"role": "user", "content": user_command}]
            )
            
            print(f"[LLM Server] Stop reason: {response.stop_reason}")
            
            # Check if Claude used a tool
            if response.stop_reason == "tool_use":
                tool_use = next(block for block in response.content if block.type == "tool_use")
                function_name = tool_use.name
                function_args = tool_use.input
                
                print(f"[LLM Server] Tool used: {function_name}")
                print(f"[LLM Server] Arguments: {function_args}")
                
                # Convert to Unity format
                result = {
                    "action": function_name.replace("_furniture", ""),  # spawn_furniture → spawn
                    **function_args
                }
                
                result.setdefault('quantity', 1)
                result.setdefault('scale', 1.0)
                
                print(f"[LLM Server] Returning: {result}")
                return jsonify(result), 200
            else:
                # Conversational response
                text_content = next((block.text for block in response.content if hasattr(block, "text")), "")
                print(f"[LLM Server] Conversational response: {text_content}")
                return jsonify({
                    "action": "query",
                    "response": text_content
                }), 200
        
        else:
            # === LOCAL MODEL APPROACH: Pattern Matching ===
            # For local Jan AI models that don't support function calling
            
            prompt = f"{SYSTEM_PROMPT}\n{user_command} →"
            
            response = client.chat.completions.create(
                model=MODEL_NAME,
                messages=[{"role": "user", "content": prompt}],
                temperature=0.0,
                max_tokens=60,
                stop=["\n"]
            )
            
            llm_text = response.choices[0].message.content.strip()
            print(f"[LLM Server] Raw response: {llm_text}")
            
            # Extract JSON from response
            json_match = re.search(r'\{.*\}', llm_text, re.DOTALL)
            
            if json_match:
                json_str = json_match.group(0)
                result = json.loads(json_str)
                
                result.setdefault('action', 'query')
                result.setdefault('quantity', 1)
                result.setdefault('scale', 1.0)
                
                print(f"[LLM Server] Parsed JSON: {result}")
                return jsonify(result), 200
            else:
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
            "response": "Error parsing response",
            "error": str(e)
        }), 200
        
    except Exception as e:
        error_msg = f"LLM Error: {str(e)}"
        print(f"[LLM Server] {error_msg}")
        return jsonify({"error": error_msg}), 500

if __name__ == '__main__':
    print("\n" + "="*60)
    print("LLM Server Starting - Structured JSON Responses")
    print("="*60)
    print(f"Server URL: http://localhost:5000")
    print(f"Jan AI URL: http://127.0.0.1:1337/v1")
    print(f"Model: Jan-v1-4B-Q4_K_M")
    print("\nFeatures:")
    print("  - Structured JSON responses")
    print("  - Color-specific spawning (blue chair, red table)")
    print("  - Delete commands")
    print("\nEndpoints:")
    print("  GET  /ping - Health check")
    print("  POST /api/process_command - Process voice commands")
    print("="*60 + "\n")
    
    app.run(host='0.0.0.0', port=5000, debug=True)
