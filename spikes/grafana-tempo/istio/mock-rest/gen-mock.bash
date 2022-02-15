#!/bin/bash

name=$1
ns=$2
shift 2
param_urls=($@)
urls='- --propagate-apis\n'
whitespace="$(echo -n '          -')"
for i in "${param_urls[@]}";do urls+="${whitespace} http://$i.$ns.svc.cluster.local\n";done
if [[ "$param_urls" != "" ]]; then
    urls=$(echo -e "${urls}")
else
    urls=' '
fi
name=$name ns=$ns urls=${urls} envsubst '${name},${ns},${urls}' < $(dirname "$(realpath $0)")/ngsa-mock.yaml
# mock_gw=$(name=$name ns=$ns envsubst '${name},${ns}' < $(dirname "$(realpath $0)")/mock-gw.yaml)


# for i in "${param_urls[@]}"; do urls+="\"http://$i.$ns.svc.cluster.local\",";done
# mock_depl=$(name=$name ns=$ns urls=${urls::-1} envsubst '${name},${ns},${urls}' < $(dirname "$(realpath $0)")/mock-deployment.yaml)
# # mock_gw=$(name=$name ns=$ns envsubst '${name},${ns}' < $(dirname "$(realpath $0)")/mock-gw.yaml)
# echo -e "$mock_depl\n---\n$mock_gw"
