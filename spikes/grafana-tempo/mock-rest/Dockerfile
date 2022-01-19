FROM python:slim as base

FROM base as builder

RUN mkdir /deps
WORKDIR /deps
COPY requirements.txt /requirements.txt

RUN pip3 install --no-cache --prefix=/deps -r /requirements.txt

FROM base as final
COPY --from=builder /deps /usr/local
COPY main.py /
EXPOSE 8421

ENTRYPOINT ["python3", "main.py"]
