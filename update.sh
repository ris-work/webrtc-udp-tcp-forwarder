#!/bin/bash

cd /home/chromebook/webrtc-udp-forwarder-git-export/webrtc-udp-tcp-forwarder
/usr/bin/fossil update
msg=$(/usr/bin/fossil timeline -n 1 | head -n -1 | tail -n -1)
msg="$msg
We had to do this because GitHub doesn't like .git directories in path 😠😥"
echo $msg
/usr/bin/git add -u
/usr/bin/git commit -m "$msg"
/usr/bin/git push --all
