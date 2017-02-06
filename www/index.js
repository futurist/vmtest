var inter1
var urlObj = window.location.href.split('/')
var protocol = urlObj[0]
var hostName = urlObj[2].split(':')
ip.value = hostName[0]
port.value = hostName[1]-100

function host (isHttp) {
  return protocol + '//' + ip.value + ':' + (isHttp ? parseInt(port.value)+100 : port.value)
}

function getStatus () {
  running.innerHTML = ''
  last_code.innerHTML = ''
  fetch(host() + '/play?status=1', {
    mode: 'cors',
    cache: 'no-cache',
    timeout: 1000
  })
    .then(function (response) {
      clearTimeout(inter1)
      inter1 = setTimeout(getStatus, 1000)
      if (response.ok) {
        if (pendingTest) {
          pendingTest = false
          action.disabled = false
          _startPlay()
        }
        return response.json()
      } else {
        throw new Error('Network response was not ok.')
      }
    })
    .catch(function () {
      clearTimeout(inter1)
      inter1 = setTimeout(getStatus, 1000)
    })
    .then(function (data) {
      if (!data) return
      running.innerHTML = data.running
      last_code.innerHTML = data.last_code == null ? 'n/a' : data.last_code
      if (data.running) {
        action.innerHTML = 'stop'
        action.onclick = stopPlay
      } else {
        action.innerHTML = 'start'
        action.onclick = startPlay
        start_result.innerHTML = ''
      }
    })
}

getStatus()

var pendingTest = false
function startPlay (isNow) {
  if (!test_name.value) return alert('select test first')
  if (reboot.checked) {
    pendingTest = true
    clearTimeout(inter1)
    inter1 = setTimeout(getStatus, 10000)
    action.disabled = true
    sendCmd('exitwin reboot')
  } else {
    _startPlay()
  }
}

function _startPlay () {
  var test = test_name.value
  if (!test) return
  fetch(host() + '/play?test=' + test)
    .then(fetchAction('json'))
    .then(function (data) {
      start_result.innerHTML = data.message
    })
}

function stopPlay () {
  fetch(host() + '/play?stop=1')
    .then(fetchAction('json'))
}

function exitServer () {
  fetch(host() + '/?exit=1')
    .then(fetchAction('text'))
}

function sendCmd (cmd) {
  fetch(host() + '/?cmd=' + encodeURIComponent(cmd))
    .then(fetchAction('text'))
}

function getDirList () {
  fetch(host() + '/dir')
    .then(fetchAction('json'))
    .then(function (data) {
      dir_list.innerHTML = data.map(function (v) {
        return '<li><span onclick=setTestName(this)>' +
          v + '</span> <a target=_blank href="' + host(true) + '/' + v + '">view</a></li>'
      }).join('')
    })
}
function setTestName (el) {
  var name = typeof el === 'string' ? el : el.innerHTML
  if (['data', 'compared'].indexOf(name) > -1) {
    return alert('cannot select data & compared')
  }
  test_name.value = name
}
getDirList()

function getShot () {
  fetch(host() + '/?snap=2')
    .then(fetchAction('text'))
    .then(function (data) {
      shot.innerHTML = data
      shot.style.display = 'block'
    })
}

function fetchAction (type) {
  return function (res) {
    if (res.ok) {
      return res[type || 'text']()
    } else {
      throw new Error('connect error')
    }
  }
}
