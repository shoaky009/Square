#!/usr/bin/env bash
# Square X11 窗口诊断脚本 (WSL / Linux)
# 用途：排查 "进程在但窗口不显示" 的问题。
# 用法(在 WSL 终端中)：
#   cd /mnt/c/Users/Wuldas/.AA/dotnet-projects/Square
#   bash diagnose-x11.sh
#
# 说明：脚本只做只读检查，不会修改系统。缺少 x11-utils 时会提示安装。

set -u

SEP="=================================================="
info()  { echo -e "\033[1;34m[INFO]\033[0m  $*"; }
ok()    { echo -e "\033[1;32m[ OK ]\033[0m  $*"; }
warn()  { echo -e "\033[1;33m[WARN]\033[0m  $*"; }
err()   { echo -e "\033[1;31m[FAIL]\033[0m  $*"; }

echo "$SEP"
info "Square X11 窗口诊断  @ $(date)"
echo "$SEP"

# 1) DISPLAY 环境变量 -------------------------------------------------------
echo
info "1) DISPLAY 环境变量"
if [ -z "${DISPLAY:-}" ]; then
  err "DISPLAY 为空！X11 客户端无法定位显示器。"
  err "=> 在 WSL 中通常由 WSLg 自动设置；若没有，请确认 Windows 侧 X Server 运行，"
  err "   并在 ~/.bashrc 中设置，例如： export DISPLAY=:0"
  echo "   若使用 VcXsrv： export DISPLAY=$(awk '/nameserver/ {print $2}' /etc/resolv.conf):0.0"
else
  ok "DISPLAY=${DISPLAY}"
fi

# 2) libX11 运行时库 --------------------------------------------------------
echo
info "2) libX11.so.6 (Square X11 后端 P/Invoke 目标)"
if ldconfig -p 2>/dev/null | grep -q 'libX11\.so\.6'; then
  ok "已找到: $(ldconfig -p 2>/dev/null | grep 'libX11\.so\.6' | head -1)"
else
  err "未找到 libX11.so.6！运行时会抛 DllNotFoundException，窗口永远无法创建。"
  err "=> 安装： sudo apt-get update && sudo apt-get install -y libx11-6"
fi

# 3) X Server 可达性 -------------------------------------------------------
echo
info "3) X Server 可达性 (xdpyinfo)"
if ! command -v xdpyinfo >/dev/null 2>&1; then
  warn "未安装 x11-utils，无法用 xdpyinfo 探测。建议： sudo apt-get install -y x11-utils"
else
  if xdpyinfo >/dev/null 2>&1; then
    ok "X Server 可达："
    xdpyinfo | grep -E "name of display|vendor|screen #0|dimensions|resolution" | sed 's/^/      /'
  else
    err "xdpyinfo 连接失败 —— X Server 未运行或 DISPLAY 指向不可达的显示器。"
  fi
fi

# 4) X Server 进程 ----------------------------------------------------------
echo
info "4) X Server / WSLg 进程"
if command -v pgrep >/dev/null 2>&1; then
  xs=$(pgrep -af 'Xwayland|Xorg|VcXsrv|Xming' 2>/dev/null | head -5)
  if [ -n "$xs" ]; then
    ok "发现 X Server 进程："
    echo "$xs" | sed 's/^/      /'
  else
    warn "未发现常见 X Server 进程 (Xwayland/Xorg/VcXsrv/Xming)。"
  fi
else
  warn "无 pgrep，跳过进程检查。"
fi

# 5) Square 进程是否在运行 -------------------------------------------------
echo
info "5) Square 示例进程"
sq=$(pgrep -af 'Square.Sample|Square\.Sample' 2>/dev/null | head -5)
if [ -n "$sq" ]; then
  ok "发现 Square 进程："
  echo "$sq" | sed 's/^/      /'
else
  warn "未发现 Square.Sample 进程。若你刚启动却看不到窗口，请确认是用 'dotnet run' 启动的，"
  warn "且启动它的终端环境里 DISPLAY 已设置（SSH 会话默认无 X 转发）。"
fi

# 6) 枚举 X11 窗口，定位 Square 窗口 --------------------------------------
echo
info "6) 枚举 X11 顶层窗口 (查找 Square)"
if command -v xlsclients >/dev/null 2>&1; then
  cl=$(xlsclients 2>/dev/null | grep -i square)
  if [ -n "$cl" ]; then
    ok "xlsclients 发现 Square 客户端："
    echo "$cl" | sed 's/^/      /'
  else
    warn "xlsclients 未发现 Square 客户端 —— 窗口可能从未被创建(连接/库失败)，"
    warn "或被创建但已被 WM 关闭。"
  fi
else
  warn "未安装 x11-utils (xlsclients)。建议： sudo apt-get install -y x11-utils"
fi

if command -v xdotool >/dev/null 2>&1; then
  ids=$(xdotool search --name 'Square' 2>/dev/null)
  if [ -n "$ids" ]; then
    echo
    ok "xdotool 找到 Square 窗口，逐一检查映射状态/几何："
    for wid in $ids; do
      map=$(xdotool getwindowgeometry --shell "$wid" 2>/dev/null)
      visible=$(xwininfo -id "$wid" 2>/dev/null | grep -i 'Map State' | tr -d ' ')
      pos=$(echo "$map" | grep -E 'X=|Y=|WIDTH=|HEIGHT=' | tr '\n' ' ')
      echo "      id=$wid  $pos  $visible"
    done
  else
    warn "xdotool 未找到名为 Square 的窗口。"
  fi
else
  warn "未安装 xdotool (可选)。如需窗口级检查： sudo apt-get install -y xdotool"
fi

# 7) 结论与建议 -------------------------------------------------------------
echo
echo "$SEP"
info "结论 / 下一步"
echo "$SEP"
echo "若第 1~3 步有 [FAIL]：先解决显示环境，再 'dotnet run'。"
echo "  常见修复："
echo "    • WSLg 未生效：在 Windows 侧确认 'WSLg' / X Server 正在运行；"
echo "      新开 WSL 终端让 DISPLAY 重新注入。"
echo "    • 缺库： sudo apt-get update && sudo apt-get install -y libx11-6 x11-utils"
echo "    • SSH 远程：启用 X 转发 (ssh -X) 或设置 DISPLAY 指向本地 X Server。"
echo "若环境全部正常但仍无窗口：请把 'dotnet run' 的 完整 stderr 输出贴给我，"
echo "重点看是否出现 'Cannot open X display' 或 'DllNotFoundException: libX11.so.6'。"
echo "$SEP"
