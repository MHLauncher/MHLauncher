import os
import json
from flask import Flask, render_template, request, jsonify
import minecraft_launcher_lib
import subprocess
import threading
import time

app = Flask(__name__)

# Путь к папке проекта — измените на ваш
BASE_DIR = r"C:\Users\DecuShunoKapushino\Desktop\Супер Носки Дракона"
os.chdir(BASE_DIR)
print(f"Текущая рабочая директория: {os.getcwd()}")

minecraft_directory = minecraft_launcher_lib.utils.get_minecraft_directory()
SETTINGS_FILE = os.path.join(BASE_DIR, 'user_settings.json')

ICONS = {
    "Vanilla": "vanilla_icon.jpg",
    "Forge": "forge_icon.jpg",
    "Fabric": "fabric_icon.jpg"
}

def create_settings_file_if_not_exists():
    if not os.path.exists(SETTINGS_FILE):
        print(f"Создаю пустой файл настроек: {SETTINGS_FILE}")
        with open(SETTINGS_FILE, 'w', encoding='utf-8') as f:
            json.dump({}, f, ensure_ascii=False, indent=4)
    else:
        print(f"Файл настроек найден: {SETTINGS_FILE}")

def load_user_settings():
    try:
        with open(SETTINGS_FILE, 'r', encoding='utf-8') as f:
            content = f.read().strip()
            if not content:
                return {}
            return json.loads(content)
    except (json.JSONDecodeError, FileNotFoundError):
        return {}

def save_user_settings(data):
    print(f"Сохраняю настройки в: {SETTINGS_FILE}")
    with open(SETTINGS_FILE, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=4)

def get_versions_by_type(version_type):
    all_versions = minecraft_launcher_lib.utils.get_available_versions(minecraft_directory)
    releases = [v for v in all_versions if v["type"] == "release"]
    versions = []
    if version_type == "Vanilla":
        for v in releases:
            versions.append({"display": v["id"], "id": v["id"], "type": "Vanilla"})
    elif version_type == "Forge":
        for v in releases:
            forge_ver = minecraft_launcher_lib.forge.find_forge_version(v["id"])
            if forge_ver:
                display_name = f"{v['id']} Forge"
                versions.append({"display": display_name, "id": forge_ver, "type": "Forge"})
    elif version_type == "Fabric":
        for v in releases:
            if minecraft_launcher_lib.fabric.is_minecraft_version_supported(v["id"]):
                display_name = f"{v['id']} Fabric"
                versions.append({"display": display_name, "id": v["id"], "type": "Fabric"})
    return versions

def get_installed_versions():
    installed = minecraft_launcher_lib.utils.get_installed_versions(minecraft_directory)
    result = []
    for v in installed:
        v_id = v["id"]
        if "forge" in v_id.lower():
            vtype = "Forge"
        elif "fabric" in v_id.lower():
            vtype = "Fabric"
        else:
            vtype = "Vanilla"
        result.append({"display": v_id, "id": v_id, "type": vtype})
    return result

def get_all_versions():
    all_versions = minecraft_launcher_lib.utils.get_available_versions(minecraft_directory)
    releases = [v for v in all_versions if v["type"] == "release"]
    all_ver_list = [v["id"] for v in releases]

    forge_ver_list = []
    fabric_ver_list = []

    for v in all_ver_list:
        forge_ver = minecraft_launcher_lib.forge.find_forge_version(v)
        if forge_ver:
            forge_ver_list.append(forge_ver)
        if minecraft_launcher_lib.fabric.is_minecraft_version_supported(v):
            fabric_ver_list.append(v)

    return {
        "all_versions": all_ver_list,
        "forge_versions": forge_ver_list,
        "fabric_versions": fabric_ver_list
    }

def get_running_status():
    settings = load_user_settings()
    return settings.get("running", None)

def set_running_status(status):
    settings = load_user_settings()
    settings["running"] = status
    save_user_settings(settings)

create_settings_file_if_not_exists()

@app.route('/', methods=['GET'])
def index():
    user_settings = load_user_settings()
    return render_template('index.html',
                           icons=ICONS,
                           user_settings=user_settings,
                           all_versions=user_settings.get("all_versions", []),
                           forge_versions=user_settings.get("forge_versions", []),
                           fabric_versions=user_settings.get("fabric_versions", []))

@app.route('/get_versions', methods=['POST'])
def get_versions():
    version_type = request.json.get('version_type', 'Vanilla')
    installed = request.json.get('installed', False)
    if installed:
        versions = get_installed_versions()
    else:
        versions = get_versions_by_type(version_type)
    return jsonify(versions)

@app.route('/download', methods=['POST'])
def download():
    version = request.json.get('version')
    vtype = request.json.get('vtype')
    try:
        if vtype == "Forge":
            vanilla_version = version.split()[0]
            minecraft_launcher_lib.install.install_minecraft_version(vanilla_version, minecraft_directory)
            forge_version = minecraft_launcher_lib.forge.find_forge_version(vanilla_version)
            minecraft_launcher_lib.forge.install_forge_version(forge_version, minecraft_directory)
        elif vtype == "Fabric":
            vanilla_version = version.split()[0]
            minecraft_launcher_lib.install.install_minecraft_version(vanilla_version, minecraft_directory)
            minecraft_launcher_lib.fabric.install_fabric(vanilla_version, minecraft_directory)
        else:
            minecraft_launcher_lib.install.install_minecraft_version(version, minecraft_directory)
        return jsonify({"status": "success", "message": f"Версия {version} успешно скачана!"})
    except Exception as e:
        return jsonify({"status": "error", "message": f"Ошибка при скачивании: {e}"})

@app.route('/launch', methods=['POST'])
def launch():
    running_status = get_running_status()
    if running_status is not None:
        return jsonify({"status": "error", "message": f"Игра уже запущена ({running_status}). Повторный запуск невозможен."})

    version = request.json.get('version')
    username = request.json.get('username')
    vtype = request.json.get('vtype')

    try:
        installed_versions = [v["id"] for v in minecraft_launcher_lib.utils.get_installed_versions(minecraft_directory)]
        real_id = version.split()[0]
        if real_id not in installed_versions:
            return jsonify({"status": "error", "message": f"Версия {real_id} не установлена. Сначала скачайте её."})

        # Помечаем, что игра запущена из web
        set_running_status("web")

        options = {
            "username": username,
            "launcherName": "WebLauncher",
            "launcherVersion": "1.0"
        }
        command = minecraft_launcher_lib.command.get_minecraft_command(real_id, minecraft_directory, options)
        subprocess.Popen(command)

        def reset_status():
            set_running_status(None)
        threading.Thread(target=reset_status, daemon=True).start()

        return jsonify({"status": "success", "message": f"Запуск версии {version} с ником {username}..."})
    except Exception as e:
        set_running_status(None)
        return jsonify({"status": "error", "message": f"Ошибка при запуске: {e}"})

@app.route('/save_settings', methods=['POST'])
def save_settings():
    data = request.json

    versions = get_all_versions()

    save_data = {
        "username": data.get("username", ""),
        "memory": data.get("memory", ""),
        "version_type": data.get("version_type", "Vanilla"),
        "version": data.get("version", ""),
        "all_versions": versions["all_versions"],
        "forge_versions": versions["forge_versions"],
        "fabric_versions": versions["fabric_versions"],
        "running": None  # всегда сбрасываем при сохранении настроек
    }

    save_user_settings(save_data)
    return jsonify({"status": "success", "message": "Настройки и версии сохранены"})

@app.route('/set_running_status', methods=['POST'])
def set_running_status_api():
    data = request.json
    status = data.get('status')  # ожидаем "app", "web" или None

    if status not in ("app", "web", None):
        return jsonify({"status": "error", "message": "Недопустимый статус"}), 400

    set_running_status(status)
    return jsonify({"status": "success", "message": f"Статус running установлен в {status}"})


# Фоновый поток для автозапуска игры при running == "app"
last_running_status = None

def background_launcher():
    global last_running_status
    while True:
        try:
            settings = load_user_settings()
            running = settings.get("running")
            version = settings.get("version")
            username = settings.get("username")
            memory = settings.get("memory")
            vtype = settings.get("version_type")

            if running == "app" and last_running_status != "app":
                print("Обнаружено задание на запуск из app! Запускаем игру...")
                installed_versions = [v["id"] for v in minecraft_launcher_lib.utils.get_installed_versions(minecraft_directory)]
                real_id = version.split()[0] if version else None
                if real_id not in installed_versions:
                    print(f"Версия {real_id} не установлена. Сначала скачайте её.")
                else:
                    options = {
                        "username": username or "Player",
                        "launcherName": "AppLauncher",
                        "launcherVersion": "1.0"
                    }
                    if memory:
                        try:
                            mem_int = int(memory)
                            options["jvmArguments"] = [f"-Xmx{mem_int}M"]
                        except:
                            pass
                        command = minecraft_launcher_lib.command.get_minecraft_command(real_id, minecraft_directory, options)
                        subprocess.Popen(command)
                        print(f"Игра {real_id} запущена с ником {username} и памятью {memory} МБ.")
                    threading.Thread(target=reset_status, daemon=True).start()

            last_running_status = running
        except Exception as e:
            print(f"Ошибка в background_launcher: {e}")
        time.sleep(1)

# Запуск фонового потока
threading.Thread(target=background_launcher, daemon=True).start()

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5002, debug=True)
