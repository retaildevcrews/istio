#!/bin/sh

echo "post-start started" >> $HOME/status

# this runs each time Codespaces starts
/workspaces/istio/clusteradm/patch.sh

echo "post-start completed" >> $HOME/status
