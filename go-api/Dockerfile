# syntax=docker/dockerfile:1.7
# Build context = ./go-api
FROM golang:1.25-alpine AS build
WORKDIR /src
COPY go.mod go.sum ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 GOOS=linux go build -trimpath -ldflags="-s -w" -o /out/go-api ./cmd/go-api

FROM alpine:3.20
RUN apk add --no-cache wget ca-certificates && adduser -D -H -u 10001 app
COPY --from=build /out/go-api /usr/local/bin/go-api
EXPOSE 8090
USER app
ENTRYPOINT ["go-api"]
