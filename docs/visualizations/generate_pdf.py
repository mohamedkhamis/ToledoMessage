#!/usr/bin/env python3
"""
PDF Generator for SecureChat Visualizations
============================================
Converts HTML visualizations to PDF format.

Requirements:
    pip install playwright
    python -m playwright install chromium

Usage:
    python generate_pdf.py

Outputs:
    - signal_protocol_baseline.pdf
    - hybrid_pq_contribution.pdf
"""

import asyncio
import os
import sys
import subprocess

def install_playwright():
    """Install playwright and browser if needed."""
    try:
        from playwright.async_api import async_playwright
        return True
    except ImportError:
        print("Installing playwright...")
        subprocess.run([sys.executable, "-m", "pip", "install", "playwright", "-q"], check=True)
        print("Installing chromium browser...")
        subprocess.run([sys.executable, "-m", "playwright", "install", "chromium"], check=True)
        return True

async def generate_pdfs():
    from playwright.async_api import async_playwright
    
    script_dir = os.path.dirname(os.path.abspath(__file__))
    
    files_to_convert = [
        {
            'input': 'signal_protocol_visual.html',
            'output': 'signal_protocol_baseline.pdf',
            'title': 'Signal Protocol Baseline Architecture'
        },
        {
            'input': 'hybrid_pq_contribution_visual.html', 
            'output': 'hybrid_pq_contribution.pdf',
            'title': 'Hybrid Post-Quantum Signal Protocol'
        }
    ]

    print("Starting PDF generation...")
    
    async with async_playwright() as p:
        browser = await p.chromium.launch(headless=True)
        
        for file in files_to_convert:
            input_path = os.path.join(script_dir, file['input'])
            output_path = os.path.join(script_dir, file['output'])
            
            if not os.path.exists(input_path):
                print(f"Error: {file['input']} not found!")
                continue
            
            print(f"Converting {file['input']} to PDF...")
            
            page = await browser.new_page()
            
            # Navigate to the local HTML file
            file_url = 'file:///' + input_path.replace('\\', '/')
            await page.goto(file_url)
            
            # Wait for animations to settle
            await page.wait_for_timeout(2000)
            
            # Generate PDF
            await page.pdf(
                path=output_path,
                format='A3',
                landscape=True,
                print_background=True,
                margin={
                    'top': '20px',
                    'bottom': '20px', 
                    'left': '20px',
                    'right': '20px'
                }
            )
            
            print(f"✓ Created: {file['output']}")
            await page.close()
        
        await browser.close()
        print("\n✅ PDF generation complete!")

def main():
    # Ensure playwright is installed
    install_playwright()
    
    # Run the async function
    asyncio.run(generate_pdfs())

if __name__ == '__main__':
    main()
