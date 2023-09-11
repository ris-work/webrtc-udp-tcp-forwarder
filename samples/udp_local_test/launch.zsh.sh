#!/bin/zsh
coproc ../../target/debug/accept-connect sample_udp_accept.toml
../../target/debug/offer-listen sample_udp.toml <&p >&p
