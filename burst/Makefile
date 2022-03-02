.PHONY: build check clean deploy

help :
	@echo "Usage:"
	@echo "   make build               - build the plug-in"
	@echo "   make check               - check the endpoints with curl"
	@echo "   make clean               - delete the istio plugin from ngsa"
	@echo "   make deploy              - deploy the istio plugin to ngsa"

deploy : clean build

	# deploy burst service
	@kubectl apply -f deploy

clean :
	# delete burst service
	@-kubectl delete -f deploy

build :
	# build burst metrics server
	@docker build . -t localhost:5000/burst:local
	@docker push localhost:5000/burst:local

run-local:
	# run burst metrics locally
	dotnet run

check :
	# get the metrics via deployment name
	@curl -q http://localhost:30081/burstmetrics/deployments/default/ngsa
	@echo ""
	# get all metrics under services
	@curl -q http://localhost:30081/burstmetrics/services
	@echo ""
