from flask import Flask, jsonify, send_file, send_from_directory
from flask_cors import CORS
import os

app = Flask(__name__)
CORS(app)  # Allow Quest to make requests

# Path to your ShapeNet GLB files
SHAPENET_PATH = r"C:\Users\s2733099\shapenet_data"

# Test endpoint - verifies connection
@app.route('/ping', methods=['GET'])
def ping():
    return jsonify({
        "status": "success",
        "message": "Flask server is running!",
        "server_ip": "192.168.178.74"
    })

# Get GLB file by category and filename
@app.route('/models/<category>/<filename>', methods=['GET'])
def get_model(category, filename):
    """
    Serves GLB files from ShapeNet dataset.
    Example: http://192.168.178.74:5000/models/chair/12345.glb
    """
    try:
        file_path = os.path.join(SHAPENET_PATH, category, filename)
        
        if not os.path.exists(file_path):
            return jsonify({
                "status": "error",
                "message": f"File not found: {category}/{filename}"
            }), 404
        
        print(f"Serving model: {category}/{filename}")
        return send_file(file_path, mimetype='model/gltf-binary')
    
    except Exception as e:
        return jsonify({
            "status": "error",
            "message": str(e)
        }), 500

# List available models in a category
@app.route('/models/<category>', methods=['GET'])
def list_models(category):
    """
    Lists all GLB files in a category.
    Example: http://192.168.178.74:5000/models/chair
    """
    try:
        category_path = os.path.join(SHAPENET_PATH, category)
        
        if not os.path.exists(category_path):
            return jsonify({
                "status": "error",
                "message": f"Category not found: {category}"
            }), 404
        
        # Get all .glb files
        files = [f for f in os.listdir(category_path) if f.endswith('.glb')]
        
        return jsonify({
            "status": "success",
            "category": category,
            "count": len(files),
            "models": files
        })
    
    except Exception as e:
        return jsonify({
            "status": "error",
            "message": str(e)
        }), 500

# List all available categories
@app.route('/categories', methods=['GET'])
def list_categories():
    """
    Lists all available ShapeNet categories.
    Example: http://192.168.178.74:5000/categories
    """
    try:
        if not os.path.exists(SHAPENET_PATH):
            return jsonify({
                "status": "error",
                "message": f"ShapeNet path not found: {SHAPENET_PATH}"
            }), 404
        
        # Get all subdirectories (categories)
        categories = [d for d in os.listdir(SHAPENET_PATH) 
                     if os.path.isdir(os.path.join(SHAPENET_PATH, d))]
        
        # Count models in each category
        category_info = {}
        for cat in categories:
            cat_path = os.path.join(SHAPENET_PATH, cat)
            model_count = len([f for f in os.listdir(cat_path) if f.endswith('.glb')])
            category_info[cat] = model_count
        
        return jsonify({
            "status": "success",
            "count": len(categories),
            "categories": category_info
        })
    
    except Exception as e:
        return jsonify({
            "status": "error",
            "message": str(e)
        }), 500

if __name__ == '__main__':
    print("=" * 60)
    print("Flask GLB Server Starting...")
    print("=" * 60)
    print(f"Server running on: http://192.168.178.74:5000")
    print(f"ShapeNet path: {SHAPENET_PATH}")
    print("")
    print("Available endpoints:")
    print("  - GET /ping")
    print("  - GET /categories")
    print("  - GET /models/<category>")
    print("  - GET /models/<category>/<filename>")
    print("")
    print("Example URLs:")
    print("  http://192.168.178.74:5000/ping")
    print("  http://192.168.178.74:5000/categories")
    print("  http://192.168.178.74:5000/models/chair")
    print("  http://192.168.178.74:5000/models/chair/12345.glb")
    print("")
    print("Press Ctrl+C to stop")
    print("=" * 60)
    
    # Run server on all network interfaces, port 5000
    # use_reloader=False prevents the double-start issue
    app.run(host='0.0.0.0', port=5000, debug=True, use_reloader=False)
