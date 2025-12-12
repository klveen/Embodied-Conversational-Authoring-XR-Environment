# LLM Integration Setup Guide

## Phase 1: Option A - Simple Text Response Testing

### Prerequisites

1. **Jan AI** must be running locally
   - Download from: https://jan.ai/
   - Start Jan AI desktop app
   - Load model: `Jan-v1-4B-Q4_K_M`
   - Jan AI server runs at: `http://127.0.0.1:1337`

2. **Python packages**
   ```bash
   pip install flask flask-cors openai
   ```

### Step 1: Start Jan AI

1. Open Jan AI desktop application
2. Go to "Settings" → "Advanced" → "Local Server"
3. Make sure server is running at `http://127.0.0.1:1337`
4. Test by opening browser: `http://127.0.0.1:1337/v1/models`
   - Should see: `{"object":"list","data":[...]}`

### Step 2: Start LLM Server

```bash
cd c:\Users\s2733099\Unity\Prototype_01\PrototypeEffort\flask_server
python llm_server.py
```

You should see:
```
============================================================
LLM Server Starting - Option A (Text Response Testing)
============================================================
Server URL: http://localhost:5000
Jan AI URL: http://127.0.0.1:1337/v1
Model: Jan-v1-4B-Q4_K_M

Endpoints:
  GET  /ping - Health check
  POST /api/process_command - Process voice commands
============================================================
```

### Step 3: Test LLM Server (Browser)

Open browser: `http://localhost:5000/ping`
- Should see: `{"message":"LLM server is running","status":"ok"}`

### Step 4: Configure Unity

1. Open Unity scene
2. Find `VoiceObjectSpawner` GameObject
3. Inspector settings:
   - ✅ Enable: `Use Python Server`
   - Set: `Python Client` → Drag PythonServerClient GameObject
4. Find `PythonServerClient` GameObject
5. Inspector settings:
   - Server URL: `http://localhost:5000`
   - LLM Endpoint: `/api/process_command`
   - Health Endpoint: `/ping`

### Step 5: Test in Unity

**Voice Command**: "test llm" or "test ai"

Expected Console Output:
```
[VoiceSpawner] Testing LLM connection with ping...
[PythonServerClient] ✓ Connection successful: {"status":"ok",...}
[VoiceSpawner] Sending test prompt to LLM...
[PythonServerClient] LLM Raw Response: {"response":"Hello! I can hear you...","command":"Hello, can you hear me?"}
[VoiceSpawner] ✓ LLM Response: Hello! I can hear you...
LLM test successful! You can now use voice commands.
```

### Step 6: Try Voice Commands (Option A)

With `Use Python Server` enabled, try:
- "spawn a chair" → LLM will respond with text (not yet spawning)
- "what objects can you create?" → LLM will explain
- "hello" → LLM will greet you

**Current behavior**: LLM returns free-form text responses. Unity logs them but doesn't spawn objects yet.

---

## Phase 2: Option B - Structured JSON (Next Step)

Once Option A works, we'll upgrade `llm_server.py` to return structured JSON:

```python
# System prompt will instruct LLM to return JSON like:
{
  "action": "spawn",
  "objectName": "chair",
  "category": "chair",
  "color": "red",
  "quantity": 1
}
```

Then Unity will parse this and actually spawn objects!

---

## Troubleshooting

### "Connection failed"
- Check Jan AI is running: `http://127.0.0.1:1337/v1/models`
- Check llm_server.py is running: `http://localhost:5000/ping`

### "LLM Error: 404"
- Jan AI model not loaded
- Open Jan AI → load `Jan-v1-4B-Q4_K_M` model

### "Module not found: openai"
```bash
pip install openai
```

### "CORS error" (if using WebGL)
- Already handled with `flask-cors`

### Server won't start
- Port 5000 already in use (maybe glb_server.py is running)
- Stop glb_server.py or change LLM server port to 5001

---

## Current Status

✅ Option A: Simple text response
- LLM receives commands
- Returns conversational text
- Unity logs responses
- **Not yet spawning objects**

⏳ Option B: Structured JSON (Next)
- Add system prompt for JSON
- Parse structured responses
- Map to spawn commands
- Handle colors, quantities, modes

