#!/bin/sh

set -e
# Get the latest report path
cur_dir=$(dirname "$0")

report_file_path=$1
[ -z ${report_file_path} ] && report_file_path=$(ls -d ${cur_dir}/TestResults/*-*-* -t | head -n 1)/coverage.cobertura.xml
report_dir=$(dirname $(dirname "$(realpath ${report_file_path})"))
echo "Target report file: $report_file_path"

test -f "$report_file_path" || { echo "Report file not found"; exit 1; }
# Show only Burst Metrics Service Classes
class_filters='+Ngsa.BurstService.K8sApi.*;+Ngsa.BurstService.Controllers.*'

reportgenerator -classfilters:"$class_filters" -reports:"${report_file_path}" -targetdir:"${report_dir}" -reporttypes:'HtmlInline;TextSummary;MarkdownSummary' \
&& cat ${report_dir}/Summary.txt
# && sed 's/|||/\n|||/g' ${report_dir}/Summary.md | mdv -
