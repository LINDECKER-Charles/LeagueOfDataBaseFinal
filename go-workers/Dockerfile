# syntax=docker/dockerfile:1.7
# Build context = ./go-workers
FROM golang:1.25-alpine AS build
WORKDIR /src
COPY go.mod ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 GOOS=linux go build -trimpath -ldflags="-s -w" -o /out/api-server ./cmd/api-server

FROM alpine:3.20
RUN apk add --no-cache wget ca-certificates && adduser -D -H -u 10001 app
COPY --from=build /out/api-server /usr/local/bin/api-server
EXPOSE 8085
USER app
ENTRYPOINT ["api-server"]
