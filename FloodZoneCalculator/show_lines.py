import sys

lines = open("Presentation/MainForm.cs", encoding="utf-8").readlines()
with open("output.txt", "w", encoding="utf-8") as f:
    for i in range(900, 1003):
        if i < len(lines):
            f.write(str(i+1) + " " + repr(lines[i][:140]) + "\n")