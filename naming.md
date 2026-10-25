# naming

## 规则

1. 全部小写，kebab-case（短横线分隔）。
2. 主标识固定为 `ahz-scribe`，角色紧跟在后面，顺序固定。
3. 同一组件在目录、Git 子模块、Docker 服务、容器、镜像、日志标签中使用同一个名字。
4. 不允许大写、下划线、空格、驼峰、别名。
5. 角色只有三个：

```text
ahz-scribe-brain
ahz-scribe-body
ahz-scribe-face
```

## 唯一事实来源

| 角色 | 目录 | Git 子模块 | Docker 服务 | 容器名 | 镜像 |
|---|---|---|---|---|---|
| 脑 | `ahz-scribe-brain` | `ahz-scribe-brain` | `ahz-scribe-brain` | `ahz-scribe-brain` | `ghcr.io/azhan524/ahz-scribe-brain:latest` |
| 身 | `ahz-scribe-body` | `ahz-scribe-body` | `ahz-scribe-body` | `ahz-scribe-body` | `ghcr.io/azhan524/ahz-scribe-body:latest` |
| 脸 | `ahz-scribe-face` | `ahz-scribe-face` | `ahz-scribe-face` | `ahz-scribe-face` | `ghcr.io/azhan524/ahz-scribe-face:latest` |

## 能力边界

```text
brain: 模型 API；不挂工作区；无 shell；只发 action-request
body:  工作区；工具；审批；审计；快照；系统调用
face:  只连 brain/body 的本地接口；不挂工作区；不直接调工具
```

## 禁止

```text
AhzScribe
ahz_scribe
ahz-scribe-Brain
brain
face
body
ahz-scribe-brain-service
```

## 校验

`tools/check-names.ps1` 会检查目录、compose 服务名/容器名、`.gitmodules` 路径。
