#!/usr/bin/env python3
"""
Convert documentation TXT files to PDF format for Unity Asset Store submission.

This script converts the text documentation files to PDF using the reportlab library.

Requirements:
    pip install reportlab

Usage:
    python convert_to_pdf.py
"""

import os
from reportlab.lib.pagesizes import A4, letter
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak, Preformatted
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.pdfgen import canvas

# Configuration
INPUT_FILES = [
    'Documentation.txt',
    'QuickStartGuide.txt',
    'ScriptReference.txt'
]

# .txt sources live here (Documentation~/DocSource, repo-only, never exported).
# Generated PDFs are written straight into the shippable Asset Store docs folder.
OUTPUT_DIR = '../../Assets/Poppod/Documentations'
PAGE_SIZE = letter  # Change to A4 if needed

def create_pdf(txt_file, pdf_file):
    """
    Convert a text file to PDF.
    
    Args:
        txt_file: Path to input .txt file
        pdf_file: Path to output .pdf file
    """
    print(f"Converting {txt_file} to {pdf_file}...")
    
    # Read the text file
    with open(txt_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Create PDF document
    doc = SimpleDocTemplate(
        pdf_file,
        pagesize=PAGE_SIZE,
        rightMargin=0.75*inch,
        leftMargin=0.75*inch,
        topMargin=0.75*inch,
        bottomMargin=0.75*inch
    )
    
    # Container for the 'Flowable' objects
    story = []
    
    # Define styles
    styles = getSampleStyleSheet()
    
    # Custom style for monospace text
    mono_style = ParagraphStyle(
        'Monospace',
        parent=styles['Normal'],
        fontName='Courier',
        fontSize=9,
        leading=11,
        leftIndent=0,
        rightIndent=0,
        spaceAfter=0,
    )
    
    # Title style
    title_style = ParagraphStyle(
        'CustomTitle',
        parent=styles['Heading1'],
        fontSize=16,
        textColor='black',
        spaceAfter=30,
        alignment=TA_CENTER,
        fontName='Helvetica-Bold'
    )
    
    # Process the content line by line
    lines = content.split('\n')
    
    for i, line in enumerate(lines):
        # Check if this is a title line (surrounded by ===)
        if '=' * 50 in line:
            # Check if next line is a title
            if i + 1 < len(lines) and lines[i + 1].strip():
                story.append(Spacer(1, 12))
                continue
            # Check if this is end of a section
            elif i > 0 and not lines[i - 1].strip():
                story.append(Spacer(1, 12))
                continue
        
        # Use Preformatted to maintain exact spacing and formatting
        if line.strip():
            # Create a preformatted paragraph that preserves spaces
            pre = Preformatted(
                line,
                mono_style,
                maxLineLength=100
            )
            story.append(pre)
        else:
            # Add small spacer for empty lines
            story.append(Spacer(1, 4))
    
    # Build PDF
    doc.build(story)
    print(f"✓ Successfully created {pdf_file}")

def main():
    """Main function to convert all documentation files."""
    print("Easy Batch Rename - Documentation to PDF Converter")
    print("=" * 60)
    print()
    
    # Check if reportlab is installed
    try:
        import reportlab
    except ImportError:
        print("ERROR: reportlab is not installed.")
        print("Please install it using: pip install reportlab")
        return
    
    # Get the script directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    assets_dir = script_dir  # Assuming script is in Assets folder
    
    # Check if we're in the right directory
    if not os.path.exists(os.path.join(assets_dir, INPUT_FILES[0])):
        # Try looking in Assets subdirectory
        assets_dir = os.path.join(script_dir, 'Assets')
        if not os.path.exists(os.path.join(assets_dir, INPUT_FILES[0])):
            print("ERROR: Could not find documentation files.")
            print(f"Looking for files in: {assets_dir}")
            print("Please run this script from the Unity project root or Assets folder.")
            return
    
    print(f"Input directory: {assets_dir}")
    print(f"Output directory: {os.path.join(assets_dir, OUTPUT_DIR)}")
    print()
    
    # Convert each file
    success_count = 0
    for txt_filename in INPUT_FILES:
        txt_path = os.path.join(assets_dir, txt_filename)
        pdf_filename = txt_filename.replace('.txt', '.pdf')
        pdf_path = os.path.join(assets_dir, OUTPUT_DIR, pdf_filename)
        
        if not os.path.exists(txt_path):
            print(f"✗ File not found: {txt_path}")
            continue
        
        try:
            create_pdf(txt_path, pdf_path)
            success_count += 1
        except Exception as e:
            print(f"✗ Error converting {txt_filename}: {e}")
    
    print()
    print("=" * 60)
    print(f"Conversion complete! {success_count}/{len(INPUT_FILES)} files converted.")
    print()
    
    if success_count == len(INPUT_FILES):
        print("All documentation files are now ready for Asset Store submission!")
        print()
        print("Next steps:")
        print("1. Review each PDF to ensure formatting is correct")
        print("2. Include PDFs in your Asset Store package")
        print("3. Reference them in your Asset Store submission")
    else:
        print("Some files could not be converted. Please check the errors above.")

if __name__ == "__main__":
    main()
