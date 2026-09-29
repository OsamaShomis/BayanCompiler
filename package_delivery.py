import os
import zipfile
import shutil
import sys
import subprocess
import tempfile

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DELIVERY_DIR = os.path.join(BASE_DIR, "delivery")
os.makedirs(DELIVERY_DIR, exist_ok=True)

GROUP_ID = "01"

def create_source_archive():
    zip_path = os.path.join(DELIVERY_DIR, f"PRFL-G{GROUP_ID}.zip")
    print(f"Creating source archive: {zip_path}")
    
    include_dirs = ["src", "tests", "samples", "report"]
    include_files = ["Bayan.sln", "publish.bat", "build_delivery.bat", "Project Instructions.md"]
    
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for f in include_files:
            p = os.path.join(BASE_DIR, f)
            if os.path.isfile(p):
                zf.write(p, arcname=f)
                
        for d in include_dirs:
            dp = os.path.join(BASE_DIR, d)
            if os.path.isdir(dp):
                for root, dirs, files in os.walk(dp):
                    dirs[:] = [sub for sub in dirs if sub.lower() not in ("bin", "obj", ".git", ".vs")]
                    for file in files:
                        fp = os.path.join(root, file)
                        rel_path = os.path.relpath(fp, BASE_DIR)
                        try:
                            zf.write(fp, arcname=rel_path)
                        except Exception as e:
                            print(f"Warning: skipped {rel_path} ({e})")
                            
    print(f"Archive created successfully: {os.path.getsize(zip_path)} bytes")

def package_report_zip():
    report_zip = os.path.join(DELIVERY_DIR, "report_latex_overleaf.zip")
    report_dir = os.path.join(BASE_DIR, "report")
    if not os.path.isdir(report_dir):
        return
    with zipfile.ZipFile(report_zip, "w", zipfile.ZIP_DEFLATED) as zf:
        for root, dirs, files in os.walk(report_dir):
            for file in files:
                fp = os.path.join(root, file)
                rel = os.path.relpath(fp, report_dir)
                zf.write(fp, arcname=rel)
    print(f"Report zip created: {os.path.getsize(report_zip)} bytes")

def create_rar_archive():
    rar_exe = r"C:\Program Files (x86)\WinRAR\Rar.exe"
    if not os.path.exists(rar_exe):
        print("Rar.exe not found, skipping RAR packaging.")
        return

    zip_path = os.path.join(DELIVERY_DIR, f"PRFL-G{GROUP_ID}.zip")
    rar_path = os.path.join(DELIVERY_DIR, f"PRFL-G{GROUP_ID}.rar")
    
    if not os.path.exists(zip_path):
        return

    print(f"Creating RAR archive via WinRAR: {rar_path}")
    with tempfile.TemporaryDirectory() as temp_dir:
        with zipfile.ZipFile(zip_path, "r") as zf:
            zf.extractall(temp_dir)
        
        if os.path.exists(rar_path):
            os.remove(rar_path)
            
        cmd = [rar_exe, "a", "-r", "-ep1", rar_path, os.path.join(temp_dir, "*")]
        subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL)
        
    print(f"RAR archive created successfully: {os.path.getsize(rar_path)} bytes")

if __name__ == "__main__":
    create_source_archive()
    package_report_zip()
    create_rar_archive()
    print("ALL DELIVERY PACKAGES CREATED CLEANLY!")
