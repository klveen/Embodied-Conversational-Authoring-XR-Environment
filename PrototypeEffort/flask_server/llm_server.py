"""
LLM Server - Claude Integration with Function Calling
Uses Claude's native tool calling for reliable structured outputs.
"""

from flask import Flask, request, jsonify, send_file
from flask_cors import CORS
import json
import os
from anthropic import Anthropic

# Model selection
MODEL_NAME = "claude-sonnet-4-0"

# Your working API key
ANTHROPIC_API_KEY = "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"

# Path to GLB files
GLB_FOLDER = r"C:\Users\s2733099\ShapenetData\GLB"
INVENTORY_CSV = r"C:\Users\s2733099\ShapenetData\inventory.csv"

app = Flask(__name__)
CORS(app)  # Enable CORS for Unity WebGL if needed

# Load inventory at startup
INVENTORY = {}  # {"chair": [{"id": "abc123", "categories": "Chair,OfficeChair"}, ...], ...}

def load_inventory():
    """Load inventory.csv and organize by main category"""
    global INVENTORY
    try:
        with open(INVENTORY_CSV, 'r', encoding='utf-8') as f:
            for line in f:
                parts = line.strip().split(',', 1)
                if len(parts) == 2:
                    model_id = parts[0].strip()
                    categories = parts[1].strip().strip('"')  # Remove quotes if present
                    main_category = categories.split(',')[0].lower()
                    
                    if main_category not in INVENTORY:
                        INVENTORY[main_category] = []
                    
                    INVENTORY[main_category].append({
                        "id": model_id,
                        "categories": categories
                    })
        
        print(f"[Inventory] Loaded {sum(len(v) for v in INVENTORY.values())} models across {len(INVENTORY)} categories")
    except Exception as e:
        print(f"[Inventory] Error loading: {e}")

load_inventory()

# Anthropic client
client = Anthropic(api_key=ANTHROPIC_API_KEY)
print(f"🤖 Using Claude API: {MODEL_NAME}")
CLAUDE_TOOLS = [
    {
        "name": "spawn_furniture",
        "description": "Spawn a piece of furniture in the AR environment. Use this when the user wants to create, place, spawn, or add furniture. Choose the most contextually appropriate model variant (e.g., OfficeChair for desk, Recliner for relaxing).",
        "input_schema": {
            "type": "object",
            "properties": {
                "objectName": {
                    "type": "string",
                    "enum": ["chair", "table", "sofa", "couch", "lamp", "bed", "desk", "shelf", "bench", "stool"],
                    "description": "The type of furniture object to spawn"
                },
                "modelId": {
                    "type": "string",
                    "description": "REQUIRED: The specific model ID from the inventory to spawn. Choose based on context and subcategories."
                },
                "color": {
                    "type": "string",
                    "enum": [
                        "red", "light_red", "dark_red",
                        "blue", "light_blue", "dark_blue",
                        "green", "light_green", "dark_green",
                        "yellow", "light_yellow", "dark_yellow",
                        "orange", "light_orange", "dark_orange",
                        "purple", "light_purple", "dark_purple",
                        "pink", "light_pink", "dark_pink",
                        "brown", "light_brown", "dark_brown",
                        "white", "black", "gray"
                    ],
                    "description": "Optional: The color to apply to the furniture. Only include if user specifies a color. Supports light and dark variations for most colors."
                },
                "quantity": {
                    "type": "integer",
                    "description": "Number of objects to spawn. Default is 1.",
                    "default": 1
                },
                "relativePosition": {
                    "type": "string",
                    "enum": ["front", "behind", "left", "right"],
                    "description": "Optional: Spawn relative to user position. Use 'front' for 'in front of me', 'behind' for 'behind me', 'left' for 'to my left', 'right' for 'to my right'. If user mentions relative position, use this instead of raycast. If not specified, use raycast placement."
                }
            },
            "required": ["objectName", "modelId"]
        }
    },
    {
        "name": "delete_furniture",
        "description": "Delete furniture from the scene. Extract objectName and color if mentioned in user's command. Examples: 'delete the pink chair' → objectName='chair', color='pink'. 'remove the table' → objectName='table'. 'delete this' → no parameters.",
        "input_schema": {
            "type": "object",
            "properties": {
                "objectName": {
                    "type": "string",
                    "enum": ["chair", "table", "sofa", "couch", "lamp", "bed", "desk", "shelf", "bench", "stool"],
                    "description": "Furniture type mentioned by user"
                },
                "color": {
                    "type": "string",
                    "enum": [
                        "red", "light_red", "dark_red",
                        "blue", "light_blue", "dark_blue",
                        "green", "light_green", "dark_green",
                        "yellow", "light_yellow", "dark_yellow",
                        "orange", "light_orange", "dark_orange",
                        "purple", "light_purple", "dark_purple",
                        "pink", "light_pink", "dark_pink",
                        "brown", "light_brown", "dark_brown",
                        "white", "black", "gray"
                    ],
                    "description": "Color mentioned by user (if any)"
                }
            },
            "required": []
        }
    },
    {
        "name": "scale_furniture",
        "description": "Scale (resize) the furniture object that the user is either holding/grabbing with their hand OR pointing at with the ray. Use this when user says 'make it bigger', 'make it smaller', 'scale up', 'resize', etc. Works with both held objects and raycast-pointed objects.",
        "input_schema": {
            "type": "object",
            "properties": {
                "scaleFactor": {
                    "type": "number",
                    "description": "The multiplier to scale the object. Examples: 1.2 for 20 percent bigger, 0.8 for 20 percent smaller, 2.0 for double size, 0.5 for half size. Must be between 0.1 and 5.0.",
                    "minimum": 0.1,
                    "maximum": 5.0
                }
            },
            "required": ["scaleFactor"]
        }
    },
    {
        "name": "modify_furniture",
        "description": "ONLY for changing the COLOR of the furniture object that the user is currently pointing at. Use this ONLY when user wants to change/modify the color (e.g., 'make it blue', 'change color to red'). DO NOT use this for requesting different models or variations - that functionality is not available.",
        "input_schema": {
            "type": "object",
            "properties": {
                "color": {
                    "type": "string",
                    "enum": [
                        "red", "light_red", "dark_red",
                        "blue", "light_blue", "dark_blue",
                        "green", "light_green", "dark_green",
                        "yellow", "light_yellow", "dark_yellow",
                        "orange", "light_orange", "dark_orange",
                        "purple", "light_purple", "dark_purple",
                        "pink", "light_pink", "dark_pink",
                        "brown", "light_brown", "dark_brown",
                        "white", "black", "gray"
                    ],
                    "description": "The new color to apply to the pointed object. Supports light and dark variations."
                }
            },
            "required": ["color"]
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
    Process voice commands using Claude's function calling
    """
    data = request.get_json()
    
    if not data or 'command' not in data:
        return jsonify({"error": "No command provided"}), 400
    
    user_command = data.get('command', '')
    print(f"\n[LLM Server] Received: {user_command}")
    
    try:
        # Build inventory context for Claude
        inventory_context = "\n\nAvailable Models:\n"
        for category, models in list(INVENTORY.items())[:10]:  # Limit to avoid token overflow
            inventory_context += f"\n{category.upper()}:\n"
            for model in models[:15]:  # Show first 15 of each category
                inventory_context += f"  - ID: {model['id']}, Types: {model['categories']}\n"
        
        response = client.messages.create(
            model=MODEL_NAME,
            max_tokens=300,
            system=f"""You are a friendly VR furniture placement assistant. Follow the users' commands to spawn, delete, modify, or scale furniture in an AR environment. Be conversational when the prompt does not require tool use.

TONE: Keep responses short, natural, and friendly. Don't over-explain unless asked.
- ✅ "Done!" or "There you go!" after simple actions
- ✅ "Making it brown for you!" when applying colors
- ✅ "I have office chairs, dining chairs, and recliners - which would you like?" when offering options
- ❌ Avoid: "I am now calling the spawn_furniture function with the following parameters..."

OFFERING OPTIONS: When user asks for furniture without specifics, mention available subcategories:
- "chair" → mention: office chairs, dining chairs, recliners, armchairs
- "table" → mention: dining tables, coffee tables, desk tables
- "sofa" → mention: sectional sofas, loveseats, 3-seater sofas
Look at the subcategories in the inventory and suggest 2-3 options naturally.

PLACEMENT HELP: If user asks how to place/spawn objects, explain:
"You can point with the raycast if you have a position far from you where you want to place, for example, a coffee table. But you can also say put a table in front of me!"

CRITICAL: When user says delete/remove + furniture description, ALWAYS call delete_furniture tool with parameters.
- "delete the pink chair" → CALL delete_furniture(objectName="chair", color="pink")
- "remove the blue table" → CALL delete_furniture(objectName="table", color="blue")  
- "delete this" → CALL delete_furniture() with no parameters

IMPORTANT: The modify_furniture tool is ONLY for changing colors.
- If user says "make this a different chair" → respond: "I can only change colors right now!"
- If user says "make it blue" → CALL modify_furniture(color="blue") + say "Done!"

MATERIAL COLORS: Translate materials to colors naturally:
- "wood" → brown (say: "Making it brown like wood!")
- "metal/steel" → gray (say: "Going with gray for that metallic look!")
- "gold" → yellow (say: "Golden yellow coming up!")
- "silver" → gray
- "bronze/copper" → orange or brown
- "marble" → white

{inventory_context}""",
            tools=CLAUDE_TOOLS,
            messages=[{"role": "user", "content": user_command}],
            tool_choice={"type": "auto"}  # Force tool use when appropriate
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
            print(f"[LLM Server] Keys in result: {list(result.keys())}")
            return jsonify(result), 200
        else:
            # Conversational response
            text_content = next((block.text for block in response.content if hasattr(block, "text")), "")
            print(f"[LLM Server] Conversational response: {text_content}")
            return jsonify({
                "action": "query",
                "response": text_content
            }), 200
        
    except Exception as e:
        error_msg = f"LLM Error: {str(e)}"
        print(f"[LLM Server] {error_msg}")
        return jsonify({"error": error_msg}), 500
@app.route('/glb/<glb_id>', methods=['GET'])
def serve_glb(glb_id):
    """Serve GLB files to Quest 3 over network"""
    try:
        # Construct path to GLB file
        glb_path = os.path.join(GLB_FOLDER, f"{glb_id}.glb")
        
        # Check if file exists
        if not os.path.exists(glb_path):
            print(f"[GLB Server] File not found: {glb_path}")
            return jsonify({"error": f"GLB file not found: {glb_id}"}), 404
        
        print(f"[GLB Server] Serving: {glb_id}.glb ({os.path.getsize(glb_path)} bytes)")
        return send_file(glb_path, mimetype='model/gltf-binary')
        
    except Exception as e:
        error_msg = f"GLB serving error: {str(e)}"
        print(f"[GLB Server] {error_msg}")
        return jsonify({"error": error_msg}), 500

@app.route('/inventory.csv', methods=['GET'])
def serve_inventory():
    """Serve inventory CSV to Quest 3"""
    try:
        if not os.path.exists(INVENTORY_CSV):
            print(f"[CSV Server] File not found: {INVENTORY_CSV}")
            return jsonify({"error": "Inventory CSV not found"}), 404
        
        print(f"[CSV Server] Serving inventory.csv ({os.path.getsize(INVENTORY_CSV)} bytes)")
        return send_file(INVENTORY_CSV, mimetype='text/csv')
        
    except Exception as e:
        error_msg = f"CSV serving error: {str(e)}"
        print(f"[CSV Server] {error_msg}")
        return jsonify({"error": error_msg}), 500

if __name__ == '__main__':
    print("\n" + "="*60)
    print("LLM SERVER - CLAUDE FUNCTION CALLING MODE")
    print("="*60)
    print(f"Server URL: http://localhost:5000")
    print(f"Model: {MODEL_NAME}")
    print(f"Inventory: {sum(len(v) for v in INVENTORY.values())} models in {len(INVENTORY)} categories")
    print("\nFeatures:")
    print("  - Claude-powered contextual model selection")
    print("  - Color-specific spawning (blue chair, red table)")
    print("  - Delete and modify commands")
    print("\nEndpoints:")
    print("  GET  /ping - Health check")
    print("  POST /api/process_command - Process voice commands")
    print("  GET  /glb/<id> - Serve GLB files to Quest")
    print("  GET  /inventory.csv - Serve inventory CSV")
    print(f"\nGLB Folder: {GLB_FOLDER}")
    print(f"Inventory CSV: {INVENTORY_CSV}")
    print("="*60 + "\n")
    
    app.run(host='0.0.0.0', port=5000, debug=True)
