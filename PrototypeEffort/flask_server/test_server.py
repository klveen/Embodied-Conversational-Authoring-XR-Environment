from flask import Flask, jsonify, send_file
from flask_cors import CORS
import os

app = Flask(__name__)
CORS(app)  # Allow Quest to make requests

# Test endpoint - verifies connection
@app.route('/ping', methods=['GET'])
def ping():
    return jsonify({
        "status": "success",
        "message": "Flask server is running!",
        "server_ip": "192.168.178.74"
    })

# Test endpoint - returns simple text
@app.route('/hello', methods=['GET'])
def hello():
    return "Hello from PC Flask Server!"

if __name__ == '__main__':
    print("=" * 60)
    print("Flask Test Server Starting...")
    print("=" * 60)
    print(f"Server running on: http://192.168.178.74:5000")
    print(f"Test from Quest: http://192.168.178.74:5000/ping")
    print(f"Press Ctrl+C to stop")
    print("=" * 60)
    
    # Run server on all network interfaces, port 5000
    app.run(host='0.0.0.0', port=5000, debug=True)
