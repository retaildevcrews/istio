#!/bin/bash

usage() {
    echo "usage:$0  [-san|--domains <DOMAINS>]  [--cert-path <PATH>]  [--cert-prefix <FILE-PREFIX>]  [--install-cert]"
    echo """
    -san | --domains <DOMAINS>  Comma separated domain list (default: localhost)
    --cert-path <PATH>          Path where certs will be written (default: pwd)
    --cert-prefix <FILE-PREFIX> Cert file name prefix (default: ngsa_cosmos)
    --install                   If set then install the cert in the system.
                                `sudo` required. (default: false)
    """
}

domain_names=localhost
cert_path=.
cert_prefix=nginx_cosmos
while [ $# -gt 0 ]; do
    opt="$1" value="$2"
    case "$opt" in
        --install-cert)
            install_cert=true;;
        -san|--domains) # comma separated domain list
            domain_names="$value";;
        --cert-path) # cert output dir
            cert_path="$value";;
        --cert-prefix) # cert file prefix
            cert_prefix="$value";;
        -h|--help)
            usage;exit 0;;
        *)
            usage; exit 1;;
    esac
    shift 2
done

IFS=',' domain_names_tkn=( $domain_names )
cert_dns_names=$(printf 'DNS:%s,' ${domain_names_tkn[@]} )
echo """Will generate certificates for the following domains (SAN):
    ${domain_names[@]}"""
# And Common Name (CN): ${wild_domain_val}"
# exit 0
mkdir -p "${cert_path}"
cert_full_path=${cert_path}/${cert_prefix}

set -e
subjectives="/CN=*/O=Microsoft Corporation/ST=Texas/C=US/L=Local development"
openssl req -x509 -newkey rsa:4096 -sha256 \
    -days 3650 -nodes -keyout "${cert_full_path}.key" \
    -out "${cert_full_path}.crt" -subj "${subjectives}"\
    -addext "subjectAltName=${cert_dns_names::-1}"

echo "Certs generated: ${cert_full_path}.key and ${cert_full_path}.crt file"
if [[ ! -z $install_cert ]]; then
    echo "Removing old certificates"
    sudo rm /usr/local/share/ca-certificates/${cert_prefix}.* /usr/share/ca-certificates/${cert_prefix}.* /etc/ssl/certs/${cert_prefix}.*

    sudo update-ca-certificates
    sudo find -L /etc/ssl/certs/ -maxdepth 1 -type l -delete

    echo "Adding generated crt to ca-certificates"
    sudo cp ${cert_full_path}.crt /usr/local/share/ca-certificates/
    sudo cp ${cert_full_path}.crt /usr/share/ca-certificates/

    echo "Updating ca-certificates"
    sudo update-ca-certificates
fi
