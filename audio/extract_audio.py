#!/usr/bin/env python3
"""
Extracts FLAC audio samples from G-Audio core.bytes files
"""

import os
import struct
import re
from pathlib import Path

GAME_SOUNDS_DIR = "put/your/actual/path/here/share/Steam/steamapps/common/Mini Motorways/Mini Motorways_Data/StreamingAssets/Sounds"
OUTPUT_DIR = "put/your/actual/path/here/Documents/code bases and repo/mini motorways decomp/audio"
LOADER_FILE = "put/your/actual/path/here/Documents/code bases and repo/mini motorways decomp/srcdump/App/Motorways/Audio/AudioDatabaseLoader.cs"

def parse_audio_database_loader():
    """Parse AudioDatabaseLoader.cs to extract sample info"""
    with open(LOADER_FILE, 'r') as f:
        content = f.read()
    
    pattern = r'AddSampleData\("([^"]+)",\s*(\d+),\s*(\d+)\)'
    
    banks = {
        24000: [],
        44100: [],
        48000: []
    }
    
    current_bank = None
    
    lines = content.split('\n')
    for line in lines:
        if 'CreateDataBank("core", 24000' in line:
            current_bank = 24000
        elif 'CreateDataBank("core", 44100' in line:
            current_bank = 44100
        elif 'CreateDataBank("core", 48000' in line:
            current_bank = 48000
        
        match = re.search(pattern, line)
        if match and current_bank:
            name = match.group(1)
            offset = int(match.group(2))
            length = int(match.group(3))
            banks[current_bank].append((name, offset, length))
    
    return banks

def extract_flac_samples(sample_rate, samples, output_subdir):
    """Extract samples as FLAC files"""
    core_file = os.path.join(GAME_SOUNDS_DIR, str(sample_rate), "core.bytes")
    
    if not os.path.exists(core_file):
        print(f"  Warning: {core_file} not found")
        return 0
    
    output_path = os.path.join(output_subdir, str(sample_rate))
    os.makedirs(output_path, exist_ok=True)
    
    with open(core_file, 'rb') as f:
        data = f.read()
    
    extracted = 0
    for name, offset, length in samples:
        try:
            
            if offset + 4 > len(data):
                continue
                
            flac_length = struct.unpack('<I', data[offset:offset+4])[0]
            
            flac_start = offset + 4
            flac_end = flac_start + flac_length
            
            if flac_end > len(data):
                flac_data = data[offset:offset + length]
            else:
                flac_data = data[flac_start:flac_end]
            
            
            if len(flac_data) >= 4 and flac_data[0:4] == b'fLaC':
                
                flac_file = os.path.join(output_path, f"{name}.flac")
                with open(flac_file, 'wb') as out:
                    out.write(flac_data)
                extracted += 1
            else:
                
                chunk = data[offset:offset + length]
                
                flac_pos = chunk.find(b'fLaC')
                if flac_pos != -1:
                    flac_data = chunk[flac_pos:]
                    flac_file = os.path.join(output_path, f"{name}.flac")
                    with open(flac_file, 'wb') as out:
                        out.write(flac_data)
                    extracted += 1
                else:
                    
                    raw_file = os.path.join(output_path, f"{name}.raw")
                    with open(raw_file, 'wb') as out:
                        out.write(chunk)
            
        except Exception as e:
            print(f"  Error extracting {name}: {e}")
    
    return extracted

def main():
    print("Mini Motorways Audio Extractor")
    
    # Parse the audio database
    print("\n[1/3] Parsing AudioDatabaseLoader.cs...")
    banks = parse_audio_database_loader()
    
    for rate, samples in banks.items():
        print(f"  Found {len(samples)} samples at {rate} Hz")
    
    # Clean old extractions
    for rate in [24000, 44100, 48000]:
        rate_dir = os.path.join(OUTPUT_DIR, str(rate))
        if os.path.exists(rate_dir):
            for f in os.listdir(rate_dir):
                os.remove(os.path.join(rate_dir, f))
    
    # Extract from each bank
    print("\n[2/3] Extracting FLAC samples...")
    total_extracted = 0
    
    for sample_rate, samples in banks.items():
        if samples:
            print(f"\n  Extracting {sample_rate} Hz bank ({len(samples)} samples)...")
            extracted = extract_flac_samples(sample_rate, samples, OUTPUT_DIR)
            total_extracted += extracted
            print(f"  Extracted {extracted} FLAC files")
    
    print(f"OK {total_extracted} total FLAC files")
    print(f"Output directory: {OUTPUT_DIR}")
if __name__ == "__main__":
    main()
