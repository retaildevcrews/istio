# Istio Filter

this is the first stab at getting filters working in the code spaces environment. it is not intended as final solution but as a template for integrating afilter as the team sees fit.




Steps 


1) source senv.sh  #this switches thte path and sets istio to 1.10.2
2) make -f Makelocal build #this builds everything 
3) source tcall.sh # this exports all the ports in use by apps behind gateway to env vars.
4) edit cmdemoyml/filter.yml #change the port and ip to what you see in the $GATEWAY_URL variable
5) edit the src/lib.rs line 118 and put in correct IP #switch the IP address in code
6) ./patch.sh  #this recompiles the lib.rs, resets the config map and kicks the ngsa service
7) kubectl get pods #do this until everything looks up
8) kubectl apply -f cmdemoyml/filter.yml  #this actually tells istio/envoy to use the filter 
9) curl -v http://$GATEWAY_URL/healthz #this calls the ngsa app. look for the header you know ngsa did not put in.
