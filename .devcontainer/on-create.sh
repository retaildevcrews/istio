#!/bin/sh

echo "on-create started" >> $HOME/status

cd clusteradm
make from-scratch

cd ..
make deploy

echo "on-create completed" > $HOME/status
