EASY THAI FONT ADJUSTMENT - DOCUMENTATION FILES
========================================

This folder contains documentation files for Easy Thai Font Adjustment.

IMPORTANT FOR ASSET STORE SUBMISSION:
--------------------------------------

Unity Asset Store requires documentation in PDF or RTF format.

The following .txt files are included:
   - Documentation.txt       (Complete manual)
   - QuickStartGuide.txt     (Getting started guide)
   - ScriptReference.txt     (Technical reference)

TO CREATE PDF FILES:
--------------------

Option 1: Word Processor - open each .txt in Word/Google Docs/LibreOffice,
   use a monospace font (Courier New / Consolas / Monaco), A4/Letter, export PDF.

Option 2: Online TXT to PDF converter.

Option 3: Command line (macOS/Linux):
   brew install pandoc
   pandoc Documentation.txt -o Documentation.pdf
   pandoc QuickStartGuide.txt -o QuickStartGuide.pdf
   pandoc ScriptReference.txt -o ScriptReference.pdf

Option 4: Python (included script):
   pip install reportlab
   python convert_to_pdf.py

ADDITIONAL DOCUMENTATION:
-------------------------

Root folder files also serve as documentation:
   - README.md           (Main project documentation - Markdown)
   - CHANGELOG.md        (Version history)
   - LICENSE             (MIT License)

QUESTIONS?
----------

   - Email: support@poppod-studio.com
   - GitHub: github.com/poppod56

========================================
