#!/bin/bash
files_with_crlf=$(find . -type f -iname '*.cs' -exec file -- {} ';' | grep CRLF)
if [ -z "$files_with_crlf" ]
then
    echo "No files found with CRLF line endings"
    exit 0
else
    echo "Files with CRLF endings:"
    echo "$files_with_crlf"
    exit 1
fi
