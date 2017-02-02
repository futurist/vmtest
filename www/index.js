var inter1

function host () {
  return ip.value + ':' + port.value
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
        return response.json()
      } else {
        throw new Error('Network response was not ok.')
      }
    })
    .catch(function() {
      clearTimeout(inter1)
      inter1 = setTimeout(getStatus, 1000)
    })
    .then(function (data) {
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

function startPlay () {
  fetch(host() + '/play?test=test')
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
